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
    class GlobalCommandTests : CommandTestsBase<GlobalCommand>
    {
        string _apiEndpoint;
        string _include;

        protected override GlobalCommand MakeCommand()
        {
            return new GlobalCommand(_collaborationEngine, _logger, _fileSettings, _realCollaborationFactory);
        }

        protected override void ConfigureCommand(GlobalCommand command)
        {
            command.PersonalAccessToken = "testToken";
            if (_apiEndpoint != null) command.ApiEndpoint = _apiEndpoint;
            if (_include != null) command.Include = _include;
        }

        [TearDown]
        public void TearDown()
        {
            _apiEndpoint = null;
            _include = null;
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
            command.PersonalAccessToken = "testToken";
            command.Include = "testRepos";
            command.ApiEndpoint = "https://github.contoso.com";

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
            command.PersonalAccessToken = "testToken";
            command.Include = "testRepos";
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
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

            var (settings, platformSettings) = await CaptureSettings(fileSettings);

            Assert.That(platformSettings, Is.Not.Null);
            Assert.That(platformSettings.Token, Is.Not.Null);
            Assert.That(platformSettings.Token, Is.EqualTo("testToken"));
            Assert.That(platformSettings.BaseApiUrl, Is.Not.Null);
            Assert.That(platformSettings.BaseApiUrl.ToString(), Is.EqualTo("http://github.contoso.com/"));


            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings, Is.Not.Null);
            Assert.That(settings.SourceControlServerSettings.Repository, Is.Null);
            Assert.That(settings.SourceControlServerSettings.OrganisationName, Is.Null);
        }

        [Test]
        public async Task EmptyFileResultsInRequiredParams()
        {
            var fileSettings = FileSettings.Empty();
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings, Is.Not.Null);
            Assert.That(settings.UserSettings.MaxRepositoriesChanged, Is.EqualTo(10));

            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.PackageFilters.Includes, Is.Not.Null);
            Assert.That(settings.PackageFilters.Includes.ToString(), Is.EqualTo("testRepos"));

            Assert.That(settings.BranchSettings, Is.Not.Null);
        }

        [Test]
        public async Task EmptyFileResultsInDefaultSettings()
        {
            var fileSettings = FileSettings.Empty();
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.UserSettings, Is.Not.Null);
            Assert.That(settings.BranchSettings, Is.Not.Null);

            Assert.That(settings.PackageFilters.MinimumAge, Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(settings.PackageFilters.Excludes, Is.Null);
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(3));

            Assert.That(settings.UserSettings.AllowedChange, Is.EqualTo(VersionChange.Major));
            Assert.That(settings.UserSettings.NuGetSources, Is.Null);
            Assert.That(settings.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.Console));
            Assert.That(settings.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Text));
            Assert.That(settings.UserSettings.CommitMessageTemplate, Is.Null);
            Assert.That(settings.UserSettings.Context, Is.Empty);

            Assert.That(settings.BranchSettings.BranchNameTemplate, Is.Null);
            Assert.That(settings.BranchSettings.DeleteBranchAfterMerge, Is.EqualTo(true));

            Assert.That(settings.SourceControlServerSettings.Scope, Is.EqualTo(ServerScope.Global));
            Assert.That(settings.SourceControlServerSettings.IncludeRepos, Is.Null);
            Assert.That(settings.SourceControlServerSettings.ExcludeRepos, Is.Null);
        }

        [Test]
        public async Task WillReadApiFromFile()
        {
            var fileSettings = new FileSettings
            {
                Api = "http://github.fish.com/"
            };

            var (_, platformSettings) = await CaptureSettings(fileSettings);

            Assert.That(platformSettings, Is.Not.Null);
            Assert.That(platformSettings.BaseApiUrl, Is.Not.Null);
            Assert.That(platformSettings.BaseApiUrl, Is.EqualTo(new Uri("http://github.fish.com/")));
        }

        [Test]
        public async Task WillReadLabelFromFile()
        {
            var fileSettings = new FileSettings
            {
                Label = new List<string> { "testLabel" }
            };
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

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
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

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
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

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
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

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
            _apiEndpoint = "http://github.contoso.com/";
            _include = "testRepos";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.BranchSettings, Is.Not.Null);
            Assert.That(settings.BranchSettings.BranchNameTemplate, Is.EqualTo(testTemplate));
        }
    }
}
