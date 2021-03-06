using McMaster.Extensions.CommandLineUtils;
using NuKeeper.Abstractions;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Collaboration;
using NuKeeper.Inspection.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuKeeper.Commands
{
    internal abstract class CollaborationPlatformCommand : CommandBase
    {
        private readonly ICollaborationEngine _engine;
        public readonly ICollaborationFactory CollaborationFactory;

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

        [Option(CommandOptionType.MultipleValue, ShortName = "r", LongName = "reviewer",
            Description =
                "Email address of reviewer to add to pull requests. Multiple reviewers can be provided by specifying this option multiple times.")]
        public List<string> Reviewers { get; set; }

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

        [Option(CommandOptionType.SingleValue, ShortName = "prtt", LongName = "pullrequesttitletemplate",
            Description = "Mustache template used for creating the pull request title.")]
        public string PullRequestTitleTemplate { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "prbt", LongName = "pullrequestbodytemplate",
            Description = "Mustache template used for creating the pull request body.")]
        public string PullRequestBodyTemplate { get; set; }

        private HashSet<Platform> _platformsSupportingDeleteBranchAfterMerge = new HashSet<Platform>();

        protected CollaborationPlatformCommand(
            ICollaborationEngine engine,
            IConfigureLogger logger,
            IFileSettingsCache fileSettingsCache,
            ICollaborationFactory collaborationFactory
        ) : base(logger, fileSettingsCache)
        {
            _engine = engine;
            CollaborationFactory = collaborationFactory;
            _platformsSupportingDeleteBranchAfterMerge.Add(Abstractions.CollaborationPlatform.Platform.AzureDevOps);
            _platformsSupportingDeleteBranchAfterMerge.Add(Abstractions.CollaborationPlatform.Platform.Bitbucket);
            _platformsSupportingDeleteBranchAfterMerge.Add(Abstractions.CollaborationPlatform.Platform.GitLab);
            _platformsSupportingDeleteBranchAfterMerge.Add(Abstractions.CollaborationPlatform.Platform.Gitea);
        }

        protected override async Task<int> Run(SettingsContainer settings)
        {
            await _engine.Run(settings);
            return 0;
        }

        protected override async Task<ValidationResult> PopulateSettings(SettingsContainer settings)
        {
            var baseResult = await base.PopulateSettings(settings);
            if (!baseResult.IsSuccess)
            {
                return baseResult;
            }

            var fileSettings = FileSettingsCache.GetSettings();

            var endpoint = Coalesce.FirstValueOrDefault(ApiEndpoint, fileSettings.Api, settings.SourceControlServerSettings.Repository?.ApiUri.ToString());
            var forkMode = ForkMode ?? fileSettings.ForkMode;
            var platform = Platform ?? fileSettings.Platform;

            // todo: validation?
            var pullRequestTitleTemplate = Coalesce.FirstValueOrDefault(
                PullRequestTitleTemplate,
                fileSettings.PullRequestTitleTemplate
            );
            settings.UserSettings.PullRequestTitleTemplate = pullRequestTitleTemplate;

            // todo: validation?
            var pullRequestBodyTemplate = Coalesce.FirstValueOrDefault(
                PullRequestBodyTemplate,
                fileSettings.PullRequestBodyTemplate
            );
            settings.UserSettings.PullRequestBodyTemplate = pullRequestBodyTemplate;

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var baseUri))
            {
                return ValidationResult.Failure($"Bad Api Base '{endpoint}'");
            }

            try
            {
                var collaborationResult = await CollaborationFactory.Initialise(
                    baseUri,
                    PersonalAccessToken,
                    forkMode,
                    platform,
                    settings.UserSettings.CommitMessageTemplate,
                    settings.UserSettings.PullRequestTitleTemplate,
                    settings.UserSettings.PullRequestBodyTemplate,
                    settings.UserSettings.Context
                );

                if (!collaborationResult.IsSuccess)
                {
                    return collaborationResult;
                }
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                return ValidationResult.Failure(ex.Message);
            }

            if (CollaborationFactory.Settings.Token == null)
            {
                return ValidationResult.Failure("The required access token was not found");
            }

            var consolidate =
                Coalesce.FirstValueOrDefault(Consolidate, fileSettings.Consolidate, false);

            settings.UserSettings.ConsolidateUpdatesInSinglePullRequest = consolidate;

            const int defaultMaxPackageUpdates = 3;
            var maxPackageUpdates =
                Coalesce.FirstValueOrDefault(MaxPackageUpdates, fileSettings.MaxPackageUpdates, defaultMaxPackageUpdates);

            settings.PackageFilters.MaxPackageUpdates = maxPackageUpdates;

            const int defaultMaxOpenPullRequests = 1;
            settings.UserSettings.MaxOpenPullRequests = Coalesce.FirstValueOrDefault(
                MaxOpenPullRequests,
                fileSettings.MaxOpenPullRequests,
                consolidate ?
                    defaultMaxOpenPullRequests
                    : maxPackageUpdates
            );

            var defaultLabels = new List<string> { "nukeeper" };

            settings.SourceControlServerSettings.Labels =
                Coalesce.FirstPopulatedListOrDefault(Label, fileSettings.Label, defaultLabels);

            var deleteBranchAfterMergeValid = PopulateDeleteBranchAfterMerge(settings);
            if (!deleteBranchAfterMergeValid.IsSuccess)
            {
                return deleteBranchAfterMergeValid;
            }

            settings.SourceControlServerSettings.Reviewers = Coalesce.FirstPopulatedListOrDefault(
                Reviewers, fileSettings.Reviewers
            );

            return ValidationResult.Success;
        }

        private ValidationResult PopulateDeleteBranchAfterMerge(
            SettingsContainer settings)
        {
            var fileSettings = FileSettingsCache.GetSettings();

            bool defaultValue;

            // The default value is true, if it is supported for the corresponding platform.
            if (Platform.HasValue && !_platformsSupportingDeleteBranchAfterMerge.Contains(Platform.Value))
            {
                defaultValue = false;
            }
            else
            {
                defaultValue = true;
            }

            settings.BranchSettings.DeleteBranchAfterMerge = Coalesce.FirstValueOrDefault(DeleteBranchAfterMerge, fileSettings.DeleteBranchAfterMerge, defaultValue);

            // Ensure that the resulting DeleteBranchAfterMerge value is supported.
            if (settings.BranchSettings.DeleteBranchAfterMerge &&
                Platform.HasValue &&
                !_platformsSupportingDeleteBranchAfterMerge.Contains(Platform.Value))
            {
                return ValidationResult.Failure("Deletion of source branch after merge is currently only available for Azure DevOps, Gitlab and Bitbucket.");
            }

            return ValidationResult.Success;
        }
    }
}
