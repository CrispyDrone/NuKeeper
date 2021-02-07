using McMaster.Extensions.CommandLineUtils;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.NuGet;
using NuKeeper.Abstractions.Output;
using System.Collections.Generic;

namespace NuKeeper.Application.Collaboration.Commands.Global
{
    [Command("global", Description = "Performs version checks and generates pull requests for all repositories the provided token can access.")]
    sealed internal class GlobalCommand
    {
        [Option(CommandOptionType.SingleValue, ShortName = "c", LongName = "change",
            Description = "Allowed version change: Patch, Minor, Major. Defaults to Major.")]
        public VersionChange? AllowedChange { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "useprerelease",
            Description = "Allowed prerelease: Always, Never, FromPrerelease. Defaults to FromPrerelease.")]
        public UsePrerelease? UsePrerelease { get; set; }

        [Option(CommandOptionType.MultipleValue, ShortName = "s", LongName = "source",
            Description =
                "Specifies a NuGet package source to use during the operation. This setting overrides all of the sources specified in the NuGet.config files. Multiple sources can be provided by specifying this option multiple times.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        // ReSharper disable once MemberCanBePrivate.Global
        public string[] Source { get; }

        public NuGetSources NuGetSources => Source == null ? null : new NuGetSources(Source);

        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "age",
            Description = "Exclude updates that do not meet a minimum age, in order to not consume packages immediately after they are released. Examples: 0 = zero, 12h = 12 hours, 3d = 3 days, 2w = two weeks. The default is 7 days.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        // ReSharper disable once MemberCanBePrivate.Global
        public string MinimumPackageAge { get; }

        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "include",
            Description = "Only consider packages matching this regex pattern.")]
        public string Include { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "e", LongName = "exclude",
            Description = "Do not consider packages matching this regex pattern.")]
        public string Exclude { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "v", LongName = "verbosity",
            Description = "Sets the verbosity level of the command. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed].")]
        public LogLevel? Verbosity { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "logdestination",
            Description = "Destination for logging.")]
        public LogDestination? LogDestination { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "logfile",
            Description = "Log to the named file.")]
        public string LogFile { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "outputformat",
            Description = "Format for output.")]
        public OutputFormat? OutputFormat { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "outputdestination",
            Description = "Destination for output.")]
        public OutputDestination? OutputDestination { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "outputfile",
            Description = "File name for output.")]
        public string OutputFileName { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "branchnametemplate",
            Description = "Template used for creating the branch name.")]
        public string BranchNameTemplate { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "git", LongName = "gitclipath",
            Description = "Path to git to use instead of lib2gitsharp implementation")]
        public string GitCliPath { get; set; }

        [Argument(1, Name = "Token",
            Description = "Personal access token to authorise access to server.")]
        public string PersonalAccessToken { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "f", LongName = "fork",
            Description =
                "Prefer to make branches on a fork of the writer repository, or on that repository itself. Allowed values are PreferFork, PreferSingleRepository, SingleRepositoryOnly.")]
        public ForkMode? ForkMode { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "m", LongName = "maxpackageupdates",
            Description = "The maximum number of package updates to apply on one repository. Defaults to 3.")]
        public int? MaxPackageUpdates { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "maxopenpullrequests",
            Description = "The maximum number of open pull requests for one repository. Defaults to 1 if `--consolidate` is specified, otherwise defaults to `--maxpackageupdates`.")]
        public int? MaxOpenPullRequests { get; set; }

        [Option(CommandOptionType.NoValue, ShortName = "n", LongName = "consolidate",
            Description = "Consolidate updates into a single pull request. Defaults to false.")]
        public bool? Consolidate { get; set; }

        [Option(CommandOptionType.MultipleValue, ShortName = "l", LongName = "label",
            Description =
                "Label to apply to GitHub pull requests. Defaults to 'nukeeper'. Multiple labels can be provided by specifying this option multiple times.")]
        public List<string> Label { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "g", LongName = "api",
            Description =
                "Api Base Url. If you are using an internal server and not a public one, you must set it to the api url of your server.")]
        public string ApiEndpoint { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "platform",
            Description = "Sets the collaboration platform type. By default this is inferred from the Url.")]
        public Platform? Platform { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "d", LongName = "deletebranchaftermerge",
            Description = "Deletes branch created by NuKeeper after merge. Defaults to true.")]
        public bool? DeleteBranchAfterMerge { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "includerepos", Description = "Only consider repositories matching this regex pattern.")]
        public string IncludeRepos { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "excluderepos", Description = "Do not consider repositories matching this regex pattern.")]
        public string ExcludeRepos { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "maxrepo",
            Description = "The maximum number of repositories to change. Defaults to 10.")]
        public int? MaxRepositoriesChanged { get; set; }
    }
}
