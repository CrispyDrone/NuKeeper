using NuKeeper.Abstractions;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Formats;
using NuKeeper.Abstractions.Logging;
using NuKeeper.AzureDevOps;
using NuKeeper.BitBucket;
using NuKeeper.BitBucketLocal;
using NuKeeper.Gitea;
using NuKeeper.GitHub;
using NuKeeper.Gitlab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuKeeper.Collaboration
{
    public class CollaborationFactory : ICollaborationFactory
    {
        private readonly IEnumerable<ISettingsReader> _settingReaders;
        private readonly INuKeeperLogger _nuKeeperLogger;
        private readonly CommitUpdateMessageTemplate _commitMessageTemplate;
        private readonly ITemplateValidator _templateValidator;
        private Platform? _platform;
        private string _commitTemplate;
        private string _pullrequestTitleTemplate;
        private string _pullrequestBodyTemplate;
        private IDictionary<string, object> _templateContext;

        public CollaborationFactory(
            IEnumerable<ISettingsReader> settingReaders,
            INuKeeperLogger nuKeeperLogger,
            CommitUpdateMessageTemplate commitMessageTemplate,
            ITemplateValidator templateValidator
        )
        {
            _settingReaders = settingReaders;
            _nuKeeperLogger = nuKeeperLogger;
            _commitMessageTemplate = commitMessageTemplate;
            _templateValidator = templateValidator;
            Settings = new CollaborationPlatformSettings();
        }

        public IForkFinder ForkFinder { get; private set; }
        public IRepositoryDiscovery RepositoryDiscovery { get; private set; }
        public ICollaborationPlatform CollaborationPlatform { get; private set; }
        public CollaborationPlatformSettings Settings { get; }
        public UpdateMessageTemplate CommitTemplate => _commitMessageTemplate;
        public UpdateMessageTemplate PullRequestTitleTemplate { get; private set; }
        public UpdateMessageTemplate PullRequestBodyTemplate { get; private set; }

        public async Task<ValidationResult> Initialise(
            Uri apiEndpoint,
            string token,
            ForkMode? forkModeFromSettings,
            Platform? platformFromSettings,
            string commitTemplate = null,
            string pullrequestTitleTemplate = null,
            string pullrequestBodyTemplate = null,
            IDictionary<string, object> templateContext = null
        )
        {
            _commitTemplate = commitTemplate;
            _pullrequestTitleTemplate = pullrequestTitleTemplate;
            _pullrequestBodyTemplate = pullrequestBodyTemplate;
            _templateContext = templateContext;

            var platformSettingsReader = await FindPlatformSettingsReader(platformFromSettings, apiEndpoint);
            if (platformSettingsReader != null)
            {
                _platform = platformSettingsReader.Platform;
            }
            else
            {
                return ValidationResult.Failure($"Unable to find collaboration platform for uri {apiEndpoint}");
            }

            Settings.BaseApiUrl = UriFormats.EnsureTrailingSlash(apiEndpoint);
            Settings.Token = token;
            Settings.ForkMode = forkModeFromSettings;
            platformSettingsReader.UpdateCollaborationPlatformSettings(Settings);

            var result = await ValidateSettings();
            if (!result.IsSuccess)
            {
                return result;
            }

            CreateForPlatform();

            return ValidationResult.Success;
        }

        private async Task<ISettingsReader> FindPlatformSettingsReader(
            Platform? platformFromSettings,
            Uri apiEndpoint
        )
        {
            if (platformFromSettings.HasValue)
            {
                var reader = _settingReaders
                    .FirstOrDefault(s => s.Platform == platformFromSettings.Value);

                if (reader != null)
                {
                    _nuKeeperLogger.Normal($"Collaboration platform specified as '{reader.Platform}'");
                }

                return reader;
            }
            else
            {
                var reader = await _settingReaders
                    .FirstOrDefaultAsync(s => s.CanRead(apiEndpoint));

                if (reader != null)
                {
                    _nuKeeperLogger.Normal($"Matched uri '{apiEndpoint}' to collaboration platform '{reader.Platform}'");
                }

                return reader;
            }
        }

        private async Task<ValidationResult> ValidateSettings()
        {
            if (!Settings.BaseApiUrl.IsWellFormedOriginalString()
                || (Settings.BaseApiUrl.Scheme != "http" && Settings.BaseApiUrl.Scheme != "https"))
            {
                return ValidationResult.Failure(
                    $"Api is not of correct format {Settings.BaseApiUrl}");
            }

            if (!Settings.ForkMode.HasValue)
            {
                return ValidationResult.Failure("Fork Mode was not set");
            }

            if (string.IsNullOrWhiteSpace(Settings.Token))
            {
                return ValidationResult.Failure("Token was not set");
            }

            if (!_platform.HasValue)
            {
                return ValidationResult.Failure("Platform was not set");
            }

            if (!string.IsNullOrEmpty(_commitTemplate))
            {
                var validationResult = await _templateValidator.ValidateAsync(_commitTemplate);
                if (!validationResult.IsSuccess)
                    return validationResult;
            }

            if (!string.IsNullOrEmpty(_pullrequestTitleTemplate))
            {
                var validationResult = await _templateValidator.ValidateAsync(_pullrequestTitleTemplate);
                if (!validationResult.IsSuccess)
                    return validationResult;
            }

            if (!string.IsNullOrEmpty(_pullrequestBodyTemplate))
            {
                var validationResult = await _templateValidator.ValidateAsync(_pullrequestBodyTemplate);
                if (!validationResult.IsSuccess)
                    return validationResult;
            }

            return ValidationResult.Success;
        }

        private void CreateForPlatform()
        {
            var forkMode = Settings.ForkMode.Value;

            switch (_platform.Value)
            {
                case Platform.AzureDevOps:
                    CollaborationPlatform = new AzureDevOpsPlatform(_nuKeeperLogger);
                    RepositoryDiscovery = new AzureDevOpsRepositoryDiscovery(_nuKeeperLogger, CollaborationPlatform, Settings.Token);
                    ForkFinder = new AzureDevOpsForkFinder(CollaborationPlatform, _nuKeeperLogger, forkMode);

                    PullRequestTitleTemplate = new AzureDevOpsPullRequestTitleTemplate
                    {
                        CustomTemplate = _pullrequestTitleTemplate
                    };

                    // We go for the specific platform version of IWriteUpdateMessage
                    // here since Azure DevOps has different pull request message limits compared to other platforms.
                    PullRequestBodyTemplate = new AzureDevOpsPullRequestBodyTemplate
                    {
                        CustomTemplate = _pullrequestBodyTemplate
                    };
                    break;

                case Platform.GitHub:
                    CollaborationPlatform = new OctokitClient(_nuKeeperLogger);
                    RepositoryDiscovery = new GitHubRepositoryDiscovery(_nuKeeperLogger, CollaborationPlatform);
                    ForkFinder = new GitHubForkFinder(CollaborationPlatform, _nuKeeperLogger, forkMode);
                    break;

                case Platform.Bitbucket:
                    CollaborationPlatform = new BitbucketPlatform(_nuKeeperLogger);
                    RepositoryDiscovery = new BitbucketRepositoryDiscovery(_nuKeeperLogger);
                    ForkFinder = new BitbucketForkFinder(CollaborationPlatform, _nuKeeperLogger, forkMode);
                    PullRequestBodyTemplate = new BitbucketPullRequestBodyTemplate
                    {
                        CustomTemplate = _pullrequestBodyTemplate
                    };
                    break;

                case Platform.BitbucketLocal:
                    CollaborationPlatform = new BitBucketLocalPlatform(_nuKeeperLogger);
                    RepositoryDiscovery = new BitbucketLocalRepositoryDiscovery(_nuKeeperLogger, CollaborationPlatform, Settings);
                    ForkFinder = new BitbucketForkFinder(CollaborationPlatform, _nuKeeperLogger, forkMode);
                    break;

                case Platform.GitLab:
                    CollaborationPlatform = new GitlabPlatform(_nuKeeperLogger);
                    RepositoryDiscovery = new GitlabRepositoryDiscovery(_nuKeeperLogger, CollaborationPlatform);
                    ForkFinder = new GitlabForkFinder(CollaborationPlatform, _nuKeeperLogger, forkMode);
                    break;

                case Platform.Gitea:
                    CollaborationPlatform = new GiteaPlatform(_nuKeeperLogger);
                    RepositoryDiscovery = new GiteaRepositoryDiscovery(_nuKeeperLogger, CollaborationPlatform);
                    ForkFinder = new GiteaForkFinder(CollaborationPlatform, _nuKeeperLogger, forkMode);
                    break;

                default:
                    throw new NuKeeperException($"Unknown platform: {_platform}");
            }

            _commitMessageTemplate.CustomTemplate = _commitTemplate;
            PullRequestTitleTemplate ??= new DefaultPullRequestTitleTemplate
            {
                CustomTemplate = _pullrequestTitleTemplate
            };
            PullRequestBodyTemplate ??= new DefaultPullRequestBodyTemplate
            {
                CustomTemplate = _pullrequestBodyTemplate
            };
            InitializeTemplateContext(_commitMessageTemplate);
            //TODO test custom properties in body and title templates
            InitializeTemplateContext(PullRequestTitleTemplate);
            InitializeTemplateContext(PullRequestBodyTemplate);

            var auth = new AuthSettings(Settings.BaseApiUrl, Settings.Token, Settings.Username);
            CollaborationPlatform.Initialise(auth);

            if (ForkFinder == null ||
                RepositoryDiscovery == null ||
                CollaborationPlatform == null)
            {
                throw new NuKeeperException($"Platform {_platform} could not be initialised");
            }
        }

        private void InitializeTemplateContext(UpdateMessageTemplate template)
        {
            template.Clear();

            if (_templateContext != null)
            {
                foreach (var property in _templateContext.Keys)
                {
                    template.AddPlaceholderValue(property, _templateContext[property], persist: true);
                }
            }
        }
    }
}
