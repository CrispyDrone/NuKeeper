using NSubstitute;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Collaboration;
using NuKeeper.Commands;
using NuKeeper.Inspection.Logging;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuKeeper.Tests.Commands
{
    [TestFixture]
#pragma warning disable CA1812
    class CollaborationPlatformCommandTests : CommandTestsBase<CollaborationPlatformCommandTests.CollaborationPlatformCommandStub>
    {
        protected override void ConfigureCommand(CollaborationPlatformCommandStub command) { }

        protected override CollaborationPlatformCommandStub MakeCommand()
        {
            return new CollaborationPlatformCommandStub(
                _engine,
                _logger,
                _fileSettingsCache,
                _collaborationFactory
            )
            {
                ApiEndpoint = "http://tfs.myorganization.com/tfs/DefaultCollection/MyProject/_git/MyRepository",
                PersonalAccessToken = "mytoken"
            };
        }


        private ICollaborationEngine _engine;
        private IFileSettingsCache _fileSettingsCache;

        [SetUp]
        public void Initialize()
        {
            _engine = Substitute.For<ICollaborationEngine>();
            _logger = Substitute.For<IConfigureLogger>();
            _fileSettingsCache = Substitute.For<IFileSettingsCache>();
            _collaborationFactory = Substitute.For<ICollaborationFactory>();

            _fileSettingsCache
                .GetSettings()
                .Returns(FileSettings.Empty());
            _collaborationFactory
                .Initialise(
                    Arg.Any<Uri>(),
                    Arg.Any<string>(),
                    Arg.Any<ForkMode?>(),
                    Arg.Any<Platform?>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<Dictionary<string, object>>()
                )
                .Returns(ValidationResult.Success);
            _collaborationFactory
                .Settings
                .Returns(new CollaborationPlatformSettings { Token = "mytoken" });
        }


        [Test]
        public async Task OnExecute_ReviewersProvidedFromCli_CorrectlyPopulatesSettingsContainerWithReviewers()
        {
            var command = MakeCommand();
            _collaborationFactory
                .Settings
                .Returns(new CollaborationPlatformSettings { Token = command.PersonalAccessToken });
            command.Reviewers = new List<string> { "nukeeper@nukeeper.nukeeper" };

            await command.OnExecute();

            await _engine
                .Received()
                .Run(
                    Arg.Is<SettingsContainer>(s =>
                        s.SourceControlServerSettings.Reviewers.Contains("nukeeper@nukeeper.nukeeper")
                    )
                );
        }


        [Test]
        public async Task OnExecute_ReviewersProvidedFromFile_CorrectlyPopulatesSettingsContainerWithReviewers()
        {
            var command = MakeCommand();
            _fileSettingsCache
                .GetSettings()
                .Returns(new FileSettings { Reviewers = new List<string> { "nukeeper@nukeeper.nukeeper" } });
            _collaborationFactory
                .Settings
                .Returns(new CollaborationPlatformSettings { Token = command.PersonalAccessToken });

            await command.OnExecute();

            await _engine
                .Received()
                .Run(
                    Arg.Is<SettingsContainer>(s =>
                        s.SourceControlServerSettings.Reviewers.Contains("nukeeper@nukeeper.nukeeper")
                    )
                );
        }

        public async Task OnExecute_CustomTemplatesFromFile_CallsInitialiseCollaborationFactoryWithCustomTemplates()
        {
            var commitTemplate = "commit template";
            var prTitleTemplate = "pr title template";
            var prBodyTemplate = "pr body template";
            _fileSettings
                .GetSettings()
                .Returns(
                    new FileSettings
                    {
                        CommitMessageTemplate = commitTemplate,
                        PullRequestTitleTemplate = prTitleTemplate,
                        PullRequestBodyTemplate = prBodyTemplate
                    }
                );
            var command = MakeCommand();

            await command.OnExecute();

            await _collaborationFactory
                .Received()
                .Initialise(
                    Arg.Any<Uri>(),
                    Arg.Any<string>(),
                    Arg.Any<ForkMode?>(),
                    Arg.Any<Platform?>(),
                    commitTemplate,
                    prTitleTemplate,
                    prBodyTemplate,
                    Arg.Any<IDictionary<string, object>>()
                );
        }

        [Test]
        public async Task OnExecute_ReviewersProvidedFromCliAndFile_CorrectlyPopulatesSettingsContainerWithReviewersFromCli()
        {
            var command = MakeCommand();
            command.Reviewers = new List<string> { "notnukeeper@nukeeper.nukeeper" };
            _collaborationFactory
                .Settings
                .Returns(new CollaborationPlatformSettings { Token = command.PersonalAccessToken });
            _fileSettingsCache
                .GetSettings()
                .Returns(new FileSettings { Reviewers = new List<string> { "nukeeper@nukeeper.nukeeper" } });

            await command.OnExecute();

            await _engine
                .Received()
                .Run(
                    Arg.Is<SettingsContainer>(s =>
                        s.SourceControlServerSettings.Reviewers.Contains("notnukeeper@nukeeper.nukeeper")
                    )
                );
        }

        [Test]
        public async Task OnExecute_CustomTemplatesFromCli_CallsInitialiseCollaborationFactoryWithCustomTemplates()
        {
            var commitTemplate = "commit template";
            var prTitleTemplate = "pr title template";
            var prBodyTemplate = "pr body template";
            var command = MakeCommand();
            command.CommitMessageTemplate = commitTemplate;
            command.PullRequestTitleTemplate = prTitleTemplate;
            command.PullRequestBodyTemplate = prBodyTemplate;

            await command.OnExecute();

            await _collaborationFactory
                .Received()
                .Initialise(
                    Arg.Any<Uri>(),
                    Arg.Any<string>(),
                    Arg.Any<ForkMode?>(),
                    Arg.Any<Platform?>(),
                    commitTemplate,
                    prTitleTemplate,
                    prBodyTemplate,
                    Arg.Any<IDictionary<string, object>>()
                );
        }

        [Test]
        public async Task OnExecute_CustomTemplatesFromCliAndFile_CallsInitialiseCollaborationFactoryWithCustomTemplatesFromCli()
        {
            var commitTemplate = "commit template";
            var prTitleTemplate = "pr title template";
            var prBodyTemplate = "pr body template";
            _fileSettings
                .GetSettings()
                .Returns(
                    new FileSettings
                    {
                        CommitMessageTemplate = "commit template from file",
                        PullRequestTitleTemplate = "pr title template from file",
                        PullRequestBodyTemplate = "pr body template from file"
                    }
                );
            var command = MakeCommand();
            command.CommitMessageTemplate = commitTemplate;
            command.PullRequestTitleTemplate = prTitleTemplate;
            command.PullRequestBodyTemplate = prBodyTemplate;

            await command.OnExecute();

            await _collaborationFactory
                .Received()
                .Initialise(
                    Arg.Any<Uri>(),
                    Arg.Any<string>(),
                    Arg.Any<ForkMode?>(),
                    Arg.Any<Platform?>(),
                    commitTemplate,
                    prTitleTemplate,
                    prBodyTemplate,
                    Arg.Any<IDictionary<string, object>>()
                );
        }

        public class CollaborationPlatformCommandStub : CollaborationPlatformCommand
        {
            public CollaborationPlatformCommandStub(
                ICollaborationEngine engine,
                IConfigureLogger logger,
                IFileSettingsCache fileSettingsCache,
                ICollaborationFactory collaborationFactory
            ) : base(engine, logger, fileSettingsCache, collaborationFactory) { }
        }
    }
}
