using NSubstitute;
using NUnit.Framework;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Logging;
using NuKeeper.AzureDevOps;
using NuKeeper.Collaboration;
using NuKeeper.GitHub;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace NuKeeper.Tests.Engine
{
    [TestFixture]
    public class CollaborationFactoryTests
    {
        ITemplateValidator _templateValidator;
        INuKeeperLogger _logger;

        [SetUp]
        public void Initialize()
        {
            _templateValidator = Substitute.For<ITemplateValidator>();
            _logger = Substitute.For<INuKeeperLogger>();
        }

        private CollaborationFactory GetCollaborationFactory()
        {
            var azureUri = new Uri("https://dev.azure.com");
            var gitHubUri = new Uri("https://api.github.com");

            var settingReader1 = Substitute.For<ISettingsReader>();
            settingReader1.CanRead(azureUri).Returns(true);
            settingReader1.Platform.Returns(Platform.AzureDevOps);

            var settingReader2 = Substitute.For<ISettingsReader>();
            settingReader2.CanRead(gitHubUri).Returns(true);
            settingReader2.Platform.Returns(Platform.GitHub);

            var readers = new List<ISettingsReader> { settingReader1, settingReader2 };
            return new CollaborationFactory(readers, _logger, new CommitUpdateMessageTemplate(), _templateValidator);
        }

        [Test]
        public void UnitialisedFactoryHasNulls()
        {
            var f = GetCollaborationFactory();

            Assert.That(f, Is.Not.Null);
            Assert.That(f.CollaborationPlatform, Is.Null);
            Assert.That(f.ForkFinder, Is.Null);
            Assert.That(f.RepositoryDiscovery, Is.Null);
        }

        [Test]
        public async Task UnknownApiReturnsUnableToFindPlatform()
        {
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                    new Uri("https://unknown.com/"), null,
                    ForkMode.SingleRepositoryOnly, null);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage,
                Is.EqualTo("Unable to find collaboration platform for uri https://unknown.com/"));
        }

        [Test]
        public async Task UnknownApiCanHaveManualPlatform()
        {
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                    new Uri("https://unknown.com/"), "token",
                    ForkMode.SingleRepositoryOnly,
                    Platform.GitHub);

            Assert.That(result.IsSuccess);
            AssertGithub(collaborationFactory);
        }

        [Test]
        public async Task ManualPlatformWillOverrideUri()
        {
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                new Uri("https://api.github.myco.com"), "token",
                ForkMode.SingleRepositoryOnly,
                Platform.AzureDevOps);

            Assert.That(result.IsSuccess);
            AssertAzureDevOps(collaborationFactory);
        }

        [Test]
        public async Task AzureDevOpsUrlReturnsAzureDevOps()
        {
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(new Uri("https://dev.azure.com"), "token",
                ForkMode.SingleRepositoryOnly, null);
            Assert.That(result.IsSuccess);

            AssertAzureDevOps(collaborationFactory);
            AssertAreSameObject(collaborationFactory);
        }

        [Test]
        public async Task GithubUrlReturnsGitHub()
        {
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(new Uri("https://api.github.com"), "token",
                ForkMode.PreferFork, null);
            Assert.That(result.IsSuccess);

            AssertGithub(collaborationFactory);
            AssertAreSameObject(collaborationFactory);
        }

        [Test]
        public async Task Initialise_ValidTemplates_ReturnsSuccessValidationResult()
        {
            _templateValidator
                .ValidateAsync(Arg.Any<string>())
                .Returns(ValidationResult.Success);
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                new Uri("https://api.github.com"),
                "token",
                ForkMode.SingleRepositoryOnly,
                Platform.GitHub,
                "commit template",
                "pull request title template",
                "pull request body template"
            );

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public async Task Initialise_InvalidCommitTemplate_ReturnsFailedValidationResult()
        {
            var commitTemplate = "invalid commit template";
            _templateValidator
                .ValidateAsync(commitTemplate)
                .Returns(ValidationResult.Failure("invalid template"));
            _templateValidator
                .ValidateAsync(Arg.Is<string>(s => s != commitTemplate))
                .Returns(ValidationResult.Success);
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                new Uri("https://api.github.com"),
                "token",
                ForkMode.SingleRepositoryOnly,
                Platform.GitHub,
                commitTemplate,
                "pull request title template",
                "pull request body template"
            );

            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public async Task Initialise_InvalidPullRequestTitleTemplate_ReturnsFailedValidationResult()
        {
            var pullRequestTitleTemplate = "invalid pull request title template";
            _templateValidator
                .ValidateAsync(pullRequestTitleTemplate)
                .Returns(ValidationResult.Failure("invalid template"));
            _templateValidator
                .ValidateAsync(Arg.Is<string>(s => s != pullRequestTitleTemplate))
                .Returns(ValidationResult.Success);
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                new Uri("https://api.github.com"),
                "token",
                ForkMode.SingleRepositoryOnly,
                Platform.GitHub,
                "commit template",
                pullRequestTitleTemplate,
                "invalid pull request body template"
            );

            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public async Task Initialise_InvalidPullRequestBodyTemplate_ReturnsFailedValidationResult()
        {
            var pullRequestBodyTemplate = "invalid pull request body template";
            _templateValidator
                .ValidateAsync(pullRequestBodyTemplate)
                .Returns(ValidationResult.Failure("invalid template"));
            _templateValidator
                .ValidateAsync(Arg.Is<string>(s => s != pullRequestBodyTemplate))
                .Returns(ValidationResult.Success);
            var collaborationFactory = GetCollaborationFactory();

            var result = await collaborationFactory.Initialise(
                new Uri("https://api.github.com"),
                "token",
                ForkMode.SingleRepositoryOnly,
                Platform.GitHub,
                "commit template",
                "pull request title template",
                pullRequestBodyTemplate
            );

            Assert.That(result.IsSuccess, Is.False);
        }

        [Test]
        public async Task Initialise_CustomTemplates_InitializesTemplatesWithCustomTemplate()
        {
            var commitTemplate = "commit template";
            var pullRequestTitleTemplate = "pull request title template";
            var pullRequestBodyTemplate = "pull request body template";
            _templateValidator
                .ValidateAsync(Arg.Any<string>())
                .Returns(ValidationResult.Success);
            var collaborationFactory = GetCollaborationFactory();

            await collaborationFactory.Initialise(
                new Uri("https://api.github.com"),
                "token",
                ForkMode.SingleRepositoryOnly,
                Platform.GitHub,
                commitTemplate,
                pullRequestTitleTemplate,
                pullRequestBodyTemplate
            );

            Assert.That(collaborationFactory.CommitTemplate.Value, Is.EqualTo(commitTemplate));
            Assert.That(collaborationFactory.PullRequestTitleTemplate.Value, Is.EqualTo(pullRequestTitleTemplate));
            Assert.That(collaborationFactory.PullRequestBodyTemplate.Value, Is.EqualTo(pullRequestBodyTemplate));
        }

        [Test]
        public async Task Initialise_Context_InitializesTemplatesWithContext()
        {
            var collaborationFactory = GetCollaborationFactory();
            var context = new Dictionary<string, object> { { "company", "nukeeper" } };

            await collaborationFactory.Initialise(
                new Uri("https://api.github.com"),
                "token",
                ForkMode.SingleRepositoryOnly,
                Platform.GitHub,
                null,
                null,
                null,
                context
            );

            Assert.That(
                collaborationFactory.CommitTemplate.GetPlaceholderValue<string>("company"),
                Is.EqualTo("nukeeper")
            );
            Assert.That(
                collaborationFactory.PullRequestTitleTemplate.GetPlaceholderValue<string>("company"),
                Is.EqualTo("nukeeper")
            );
            Assert.That(
                collaborationFactory.PullRequestBodyTemplate.GetPlaceholderValue<string>("company"),
                Is.EqualTo("nukeeper")
            );
        }

        private static void AssertAreSameObject(ICollaborationFactory collaborationFactory)
        {
            var collaborationPlatform = collaborationFactory.CollaborationPlatform;
            Assert.AreSame(collaborationPlatform, collaborationFactory.CollaborationPlatform);

            var repositoryDiscovery = collaborationFactory.RepositoryDiscovery;
            Assert.AreSame(repositoryDiscovery, collaborationFactory.RepositoryDiscovery);

            var forkFinder = collaborationFactory.ForkFinder;
            Assert.AreSame(forkFinder, collaborationFactory.ForkFinder);

            var settings = collaborationFactory.Settings;
            Assert.AreSame(settings, collaborationFactory.Settings);
        }

        private static void AssertGithub(ICollaborationFactory collaborationFactory)
        {
            Assert.IsInstanceOf<GitHubForkFinder>(collaborationFactory.ForkFinder);
            Assert.IsInstanceOf<GitHubRepositoryDiscovery>(collaborationFactory.RepositoryDiscovery);
            Assert.IsInstanceOf<OctokitClient>(collaborationFactory.CollaborationPlatform);
            Assert.IsInstanceOf<CollaborationPlatformSettings>(collaborationFactory.Settings);
            Assert.IsInstanceOf<DefaultPullRequestBodyTemplate>(collaborationFactory.PullRequestBodyTemplate);
            Assert.IsInstanceOf<DefaultPullRequestTitleTemplate>(collaborationFactory.PullRequestTitleTemplate);
        }

        private static void AssertAzureDevOps(ICollaborationFactory collaborationFactory)
        {
            Assert.IsInstanceOf<AzureDevOpsForkFinder>(collaborationFactory.ForkFinder);
            Assert.IsInstanceOf<AzureDevOpsRepositoryDiscovery>(collaborationFactory.RepositoryDiscovery);
            Assert.IsInstanceOf<AzureDevOpsPlatform>(collaborationFactory.CollaborationPlatform);
            Assert.IsInstanceOf<CollaborationPlatformSettings>(collaborationFactory.Settings);
            Assert.IsInstanceOf<AzureDevOpsPullRequestBodyTemplate>(collaborationFactory.PullRequestBodyTemplate);
            Assert.IsInstanceOf<AzureDevOpsPullRequestTitleTemplate>(collaborationFactory.PullRequestTitleTemplate);
        }
    }
}
