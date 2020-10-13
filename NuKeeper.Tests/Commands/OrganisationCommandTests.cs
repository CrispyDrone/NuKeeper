using NSubstitute;
using NUnit.Framework;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Output;
using NuKeeper.AzureDevOps;
using NuKeeper.Commands;
using NuKeeper.GitHub;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace NuKeeper.Tests.Commands
{
    [TestFixture]
#pragma warning disable CA1812
    class OrganisationCommandTests : CommandTestsBase<OrganisationCommand>
    {
        string _includeRepos;
        string _excludeRepos;
        ForkMode? _forkMode;
        int? _maxRepositoryChanges;

        protected override OrganisationCommand MakeCommand()
        {
            return new OrganisationCommand(_collaborationEngine, _logger, _fileSettings, _realCollaborationFactory);
        }

        protected override void ConfigureCommand(OrganisationCommand command)
        {
            command.PersonalAccessToken = "testToken";
            command.OrganisationName = "testOrg";

            if (_includeRepos != null) command.IncludeRepos = _includeRepos;
            if (_excludeRepos != null) command.ExcludeRepos = _excludeRepos;
            if (_forkMode != null) command.ForkMode = _forkMode;
            if (_maxRepositoryChanges != null) command.MaxRepositoriesChanged = _maxRepositoryChanges;
        }

        [TearDown]
        public void TeardDown()
        {
            _includeRepos = null;
            _excludeRepos = null;
            _forkMode = null;
            _maxRepositoryChanges = null;
        }

        [Test]
        public async Task ShouldCallEngineAndNotSucceedWithoutParams()
        {
            _fileSettings.GetSettings().Returns(FileSettings.Empty());
            _realCollaborationFactory = GetCollaborationFactory((d, e) => new[] { new GitHubSettingsReader(d, e) });
            var command = MakeCommand();

            var status = await command.OnExecute();

            Assert.That(status, Is.EqualTo(-1));
            await _collaborationEngine
                .DidNotReceive()
                .Run(Arg.Any<SettingsContainer>());
        }

        [Test]
        public async Task ShouldCallEngineAndSucceedWithRequiredGithubParams()
        {
            _fileSettings.GetSettings().Returns(FileSettings.Empty());
            _realCollaborationFactory = GetCollaborationFactory((d, e) => new[] { new GitHubSettingsReader(d, e) });
            var command = MakeCommand();
            command.PersonalAccessToken = "abc";
            command.OrganisationName = "testOrg";

            var status = await command.OnExecute();

            Assert.That(status, Is.EqualTo(0));
            await _collaborationEngine
                .Received(1)
                .Run(Arg.Any<SettingsContainer>());
        }

        [Test]
        public async Task ShouldCallEngineAndSucceedWithRequiredAzureDevOpsParams()
        {
            _fileSettings.GetSettings().Returns(FileSettings.Empty());
            _realCollaborationFactory = GetCollaborationFactory((d, e) => new[] { new AzureDevOpsSettingsReader(d, e) });
            var command = MakeCommand();
            command.PersonalAccessToken = "abc";
            command.OrganisationName = "testOrg";
            command.ApiEndpoint = "https://dev.azure.com/org";

            var status = await command.OnExecute();

            Assert.That(status, Is.EqualTo(0));
            await _collaborationEngine
                .Received(1)
                .Run(Arg.Any<SettingsContainer>());
        }

        [Test]
        public async Task ShouldPopulateSettings()
        {
            var fileSettings = FileSettings.Empty();

            var (settings, platformSettings) = await CaptureSettings(fileSettings);

            Assert.That(platformSettings, Is.Not.Null);
            Assert.That(platformSettings.Token, Is.Not.Null);
            Assert.That(platformSettings.Token, Is.EqualTo("testToken"));

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.Scope, Is.EqualTo(ServerScope.Organisation));
            Assert.That(settings.SourceControlServerSettings.Repository, Is.Null);
            Assert.That(settings.SourceControlServerSettings.OrganisationName, Is.EqualTo("testOrg"));
        }

        [Test]
        public async Task EmptyFileResultsInDefaultSettings()
        {
            var fileSettings = FileSettings.Empty();

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.UserSettings, Is.Not.Null);
            Assert.That(settings.BranchSettings, Is.Not.Null);

            Assert.That(settings.PackageFilters.MinimumAge, Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(settings.PackageFilters.Excludes, Is.Null);
            Assert.That(settings.PackageFilters.Includes, Is.Null);
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(3));

            Assert.That(settings.UserSettings.AllowedChange, Is.EqualTo(VersionChange.Major));
            Assert.That(settings.UserSettings.NuGetSources, Is.Null);
            Assert.That(settings.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.Console));
            Assert.That(settings.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Text));
            Assert.That(settings.UserSettings.MaxRepositoriesChanged, Is.EqualTo(10));
            Assert.That(settings.UserSettings.CommitMessageTemplate, Is.Null);
            Assert.That(settings.UserSettings.Context, Is.Empty);

            Assert.That(settings.BranchSettings.BranchNameTemplate, Is.Null);
            Assert.That(settings.BranchSettings.DeleteBranchAfterMerge, Is.EqualTo(true));

            Assert.That(settings.SourceControlServerSettings.IncludeRepos, Is.Null);
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos, Is.Null);
        }

        [Test]
        public async Task WillReadApiFromFile()
        {
            var fileSettings = new FileSettings
            {
                Api = "http://github.contoso.com/"
            };

            var (_, platformSettings) = await CaptureSettings(fileSettings);

            Assert.That(platformSettings, Is.Not.Null);
            Assert.That(platformSettings.BaseApiUrl, Is.Not.Null);
            Assert.That(platformSettings.BaseApiUrl, Is.EqualTo(new Uri("http://github.contoso.com/")));
        }

        [Test]
        public async Task WillReadLabelFromFile()
        {
            var fileSettings = new FileSettings
            {
                Label = new List<string> { "testLabel" }
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.Labels, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.Labels, Has.Count.EqualTo(1));
            Assert.That(settings.SourceControlServerSettings.Labels, Does.Contain("testLabel"));
        }

        [Test]
        public async Task WillReadRepoFiltersFromFile()
        {
            var fileSettings = new FileSettings
            {
                IncludeRepos = "foo",
                ExcludeRepos = "bar"
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.IncludeRepos, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.IncludeRepos.ToString(), Is.EqualTo("foo"));
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos.ToString(), Is.EqualTo("bar"));
        }

        [Test]
        public async Task WillReadMaxPackageUpdatesFromFile()
        {
            var fileSettings = new FileSettings
            {
                MaxPackageUpdates = 42
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(42));
        }

        [Test]
        public async Task WillReadMaxRepoFromFile()
        {
            var fileSettings = new FileSettings
            {
                MaxRepo = 42
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.UserSettings.MaxRepositoriesChanged, Is.EqualTo(42));
        }

        [Test]
        public async Task WillReadBranchNamePrefixFromFile()
        {
            var testTemplate = "nukeeper/MyBranch";

            var fileSettings = new FileSettings
            {
                BranchNameTemplate = testTemplate
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.BranchSettings, Is.Not.Null);
            Assert.That(settings.BranchSettings.BranchNameTemplate, Is.EqualTo(testTemplate));
        }

        [Test]
        public async Task CommandLineWillOverrideIncludeRepo()
        {
            var fileSettings = new FileSettings
            {
                IncludeRepos = "foo",
                ExcludeRepos = "bar"
            };
            _includeRepos = "IncludeFromCommand";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.IncludeRepos, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.IncludeRepos.ToString(), Is.EqualTo("IncludeFromCommand"));
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos.ToString(), Is.EqualTo("bar"));
        }

        [Test]
        public async Task CommandLineWillOverrideExcludeRepo()
        {
            var fileSettings = new FileSettings
            {
                IncludeRepos = "foo",
                ExcludeRepos = "bar"
            };
            _excludeRepos = "ExcludeFromCommand";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.IncludeRepos, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.IncludeRepos.ToString(), Is.EqualTo("foo"));
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos.ToString(), Is.EqualTo("ExcludeFromCommand"));
        }

        [Test]
        public async Task CommandLineWillOverrideForkMode()
        {
            _forkMode = ForkMode.PreferSingleRepository;
            var (_, platformSettings) = await CaptureSettings(FileSettings.Empty());

            Assert.That(platformSettings, Is.Not.Null);
            Assert.That(platformSettings.ForkMode, Is.Not.Null);
            Assert.That(platformSettings.ForkMode, Is.EqualTo(ForkMode.PreferSingleRepository));
        }

        [Test]
        public async Task CommandLineWillOverrideMaxRepo()
        {
            var fileSettings = new FileSettings
            {
                MaxRepo = 12,
            };
            _excludeRepos = "ExcludeFromCommand";
            _maxRepositoryChanges = 22;

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings.MaxRepositoriesChanged, Is.EqualTo(22));
        }
    }
}
