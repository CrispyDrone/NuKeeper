using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Formats;
using NuKeeper.Abstractions.RepositoryInspection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuKeeper.BitBucket
{
    public class BitbucketPullRequestBodyTemplate : UpdateMessageTemplate
    {
        public BitbucketPullRequestBodyTemplate()
            : base(new StubbleTemplateRenderer()) { }

        //TODO
        public static string DefaultTemplate { get; } =
@"{{#multipleChanges}}{{packageCount}} packages were updated in {{projectsUpdated}} project{{#multipleProjects}}s{{/multipleProjects}}:
{{#packages}}`{{Name}}`{{^Last}}, {{/Last}}{{/packages}}

**Details of updated packages**

{{/multipleChanges}}
{{#packages}}NuKeeper has generated a {{ActualChange}} update of `{{Name}}` to `{{Version}}`{{^MultipleUpdates}} from `{{FromVersion}}`{{/MultipleUpdates}}
{{#MultipleUpdates}}{{ProjectsUpdated}} versions of `{{Name}}` were found in use: {{#Updates}}`{{FromVersion}}`{{^Last}}, {{/Last}}{{/Updates}}{{/MultipleUpdates}}
{{#Publication}}`{{Name}} {{Version}}` was published at `{{Date}}`, {{Ago}}{{/Publication}}
{{#LatestVersion}}There is also a higher version, `{{Name}} {{Version}}`{{#Publication}} published at `{{Date}}`, {{Ago}}{{/Publication}}, but this was not applied as only `{{AllowedChange}}` version changes are allowed.
{{/LatestVersion}}
{{ProjectsUpdated}} project update{{#MultipleProjectsUpdated}}s{{/MultipleProjectsUpdated}}:
{{#Updates}}
Updated `{{SourceFilePath}}` to `{{Name}}` `{{ToVersion}}` from `{{FromVersion}}`
{{/Updates}}
{{#IsFromNuget}}

[{{Name}} {{Version}} on NuGet.org]({{Url}})
{{/IsFromNuget}}
{{/packages}}
{{#multipleChanges}}

{{/multipleChanges}}
{{#footer}}
{{WarningMessage}}
**NuKeeper**: {{NuKeeperUrl}}
{{/footer}}
";

        public string CustomTemplate { get; set; }

        public override string Value => CustomTemplate ?? DefaultTemplate;

        //TODO: remove
        public static string Write(PackageUpdateSet updates)
        {
            if (updates == null) throw new ArgumentNullException(nameof(updates));

            var builder = new StringBuilder();

            builder.AppendLine(MakeCommitVersionDetails(updates));

            AddCommitFooter(builder);

            return builder.ToString();
        }

        public static string Write(IReadOnlyCollection<PackageUpdateSet> updates)
        {
            if (updates == null)
            {
                throw new ArgumentNullException(nameof(updates));
            }

            var builder = new StringBuilder();

            if (updates.Count > 1)
            {
                MultiPackagePrefix(updates, builder);
            }

            foreach (var update in updates)
            {
                builder.AppendLine(MakeCommitVersionDetails(update));
            }

            AddCommitFooter(builder);

            return builder.ToString();
        }

        private static void MultiPackagePrefix(IReadOnlyCollection<PackageUpdateSet> updates, StringBuilder builder)
        {
            var packageNames = updates
                .Select(p => CodeQuote(p.SelectedId))
                .JoinWithCommas();

            var projects = updates.SelectMany(
                    u => u.CurrentPackages)
                .Select(p => p.Path.FullName)
                .Distinct()
                .ToList();

            var projectOptS = (projects.Count > 1) ? "s" : string.Empty;

            builder.AppendLine($"{updates.Count} packages were updated in {projects.Count} project{projectOptS}:");
            builder.AppendLine(packageNames);
            builder.AppendLine("");
            builder.AppendLine("**Details of updated packages**");
            builder.AppendLine("");
        }

        private static string MakeCommitVersionDetails(PackageUpdateSet updates)
        {
            var versionsInUse = updates.CurrentPackages
                .Select(u => u.Version)
                .Distinct()
                .ToList();

            var oldVersions = versionsInUse
                .Select(v => CodeQuote(v.ToString()))
                .ToList();

            var minOldVersion = versionsInUse.Min();

            var newVersion = CodeQuote(updates.SelectedVersion.ToString());
            var packageId = CodeQuote(updates.SelectedId);

            var changeLevel = ChangeLevel(minOldVersion, updates.SelectedVersion);

            var builder = new StringBuilder();

            if (oldVersions.Count == 1)
            {
                builder.AppendLine($"NuKeeper has generated a {changeLevel} update of {packageId} to {newVersion} from {oldVersions.JoinWithCommas()}");
            }
            else
            {
                builder.AppendLine($"NuKeeper has generated a {changeLevel} update of {packageId} to {newVersion}");
                builder.AppendLine($"{oldVersions.Count} versions of {packageId} were found in use: {oldVersions.JoinWithCommas()}");
            }

            if (updates.Selected.Published.HasValue)
            {
                var packageWithVersion = CodeQuote(updates.SelectedId + " " + updates.SelectedVersion);
                var pubDateString = CodeQuote(DateFormat.AsUtcIso8601(updates.Selected.Published));
                var pubDate = updates.Selected.Published.Value.UtcDateTime;
                var ago = TimeSpanFormat.Ago(pubDate, DateTime.UtcNow);

                builder.AppendLine($"{packageWithVersion} was published at {pubDateString}, {ago}");
            }

            var highestVersion = updates.Packages.Major?.Identity.Version;
            if (highestVersion != null && (highestVersion > updates.SelectedVersion))
            {
                LogHighestVersion(updates, highestVersion, builder);
            }

            builder.AppendLine();

            if (updates.CurrentPackages.Count == 1)
            {
                builder.AppendLine("1 project update:");
            }
            else
            {
                builder.AppendLine($"{updates.CurrentPackages.Count} project updates:");
            }

            foreach (var current in updates.CurrentPackages)
            {
                var line = $"Updated {CodeQuote(current.Path.RelativePath)} to {packageId} {CodeQuote(updates.SelectedVersion.ToString())} from {CodeQuote(current.Version.ToString())}";
                builder.AppendLine(line);
            }

            if (SourceIsPublicNuget(updates.Selected.Source.SourceUri))
            {
                builder.AppendLine(NugetPackageLink(updates.Selected.Identity));
            }

            return builder.ToString();
        }

        private static void AddCommitFooter(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("This is an automated update. Merge only if it passes tests");
            builder.AppendLine("**NuKeeper**: https://github.com/NuKeeperDotNet/NuKeeper");
        }

        private static string ChangeLevel(NuGetVersion oldVersion, NuGetVersion newVersion)
        {
            if (newVersion.Major > oldVersion.Major)
            {
                return "major";
            }

            if (newVersion.Minor > oldVersion.Minor)
            {
                return "minor";
            }

            if (newVersion.Patch > oldVersion.Patch)
            {
                return "patch";
            }

            if (!newVersion.IsPrerelease && oldVersion.IsPrerelease)
            {
                return "out of beta";
            }

            return string.Empty;
        }

        private static void LogHighestVersion(PackageUpdateSet updates, NuGetVersion highestVersion, StringBuilder builder)
        {
            var allowedChange = CodeQuote(updates.AllowedChange.ToString());
            var highest = CodeQuote(updates.SelectedId + " " + highestVersion);

            var highestPublishedAt = HighestPublishedAt(updates.Packages.Major.Published);

            builder.AppendLine(
                $"There is also a higher version, {highest}{highestPublishedAt}, " +
                $"but this was not applied as only {allowedChange} version changes are allowed.");
        }

        private static string HighestPublishedAt(DateTimeOffset? highestPublishedAt)
        {
            if (!highestPublishedAt.HasValue)
            {
                return string.Empty;
            }

            var highestPubDate = highestPublishedAt.Value;
            var formattedPubDate = CodeQuote(DateFormat.AsUtcIso8601(highestPubDate));
            var highestAgo = TimeSpanFormat.Ago(highestPubDate.UtcDateTime, DateTime.UtcNow);

            return $" published at {formattedPubDate}, {highestAgo}";
        }

        private static string CodeQuote(string value)
        {
            return "`" + value + "`";
        }

        private static bool SourceIsPublicNuget(Uri sourceUrl)
        {
            return
                sourceUrl != null &&
                sourceUrl.ToString().StartsWith("https://api.nuget.org/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NugetPackageLink(PackageIdentity package)
        {
            var url = $"https://www.nuget.org/packages/{package.Id}/{package.Version}";
            return $"[{package.Id} {package.Version} on NuGet.org]({url})";
        }
    }
}
