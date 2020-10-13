using NSubstitute;
using NuKeeper.Abstractions.CollaborationModels;
using NuKeeper.Abstractions.CollaborationPlatform;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Git;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.RepositoryInspection;
using NuKeeper.Collaboration;
using NuKeeper.Commands;
using NuKeeper.GitHub;
using NuKeeper.Inspection.Logging;
using NuKeeper.Local;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuKeeper.Tests.Commands
{
    abstract class CommandTestsBase<T> where T : CommandBase
    {
        protected ILocalEngine _localEngine;
        protected INuKeeperLogger _nuKeeperLogger;
        protected ICollaborationEngine _collaborationEngine;
        protected IConfigureLogger _logger;
        protected IFileSettingsCache _fileSettings;
        protected IWriteUpdateMessage _commitWorder;
        protected IEnvironmentVariablesProvider _environmentVariablesProvider;
        protected ICollaborationFactory _collaborationFactory;
        protected ICollaborationFactory _realCollaborationFactory;
        protected ITemplateRenderer _templateRenderer;
        protected IEnrichContext<PackageUpdateSet, UpdateMessageTemplate> _enricher;
        protected ITemplateValidator _templateValidator;

        protected abstract T MakeCommand();
        protected abstract void ConfigureCommand(T command);

        [SetUp]
        public void InitializeBase()
        {
            _localEngine = Substitute.For<ILocalEngine>();
            _nuKeeperLogger = Substitute.For<INuKeeperLogger>();
            _collaborationEngine = Substitute.For<ICollaborationEngine>();
            _logger = Substitute.For<IConfigureLogger>();
            _fileSettings = Substitute.For<IFileSettingsCache>();
            _commitWorder = Substitute.For<IWriteUpdateMessage>();
            _environmentVariablesProvider = Substitute.For<IEnvironmentVariablesProvider>();
            _collaborationFactory = Substitute.For<ICollaborationFactory>();
            _templateRenderer = Substitute.For<ITemplateRenderer>();
            _enricher = Substitute.For<IEnrichContext<PackageUpdateSet, UpdateMessageTemplate>>();
            _templateValidator = Substitute.For<ITemplateValidator>();

            _fileSettings
                .GetSettings()
                .Returns(FileSettings.Empty());
        }

        protected async Task<(
            SettingsContainer settingsContainer,
            CollaborationPlatformSettings platformSettings
        )> CaptureSettings(FileSettings settingsIn)
        {
            _fileSettings.GetSettings().Returns(settingsIn);
            _realCollaborationFactory = GetCollaborationFactory((d, e) => new[] { new GitHubSettingsReader(d, e) });
            SettingsContainer settingsOut = null;

            await _localEngine.Run(Arg.Do<SettingsContainer>(x => settingsOut = x), Arg.Any<bool>());
            await _collaborationEngine.Run(Arg.Do<SettingsContainer>(x => settingsOut = x));

            var command = MakeCommand();
            ConfigureCommand(command);

            await command.OnExecute();

            return (settingsOut, _realCollaborationFactory.Settings);
        }

        protected CollaborationFactory GetCollaborationFactory(
            Func<IGitDiscoveryDriver, IEnvironmentVariablesProvider, IEnumerable<ISettingsReader>> createSettingsReaders
        )
        {
            return new CollaborationFactory(
                createSettingsReaders(
                    new MockedGitDiscoveryDriver(),
                    _environmentVariablesProvider
                ),
                _nuKeeperLogger,
                new CommitUpdateMessageTemplate(),
                _templateValidator
            );
        }
    }
}
