using System;
using System.Threading.Tasks;
using NSubstitute;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Output;
using NuKeeper.Commands;
using NUnit.Framework;

namespace NuKeeper.Tests.Commands
{
    [TestFixture]
#pragma warning disable CA1812
    class UpdateCommandTests : CommandTestsBase<UpdateCommand>
    {
        VersionChange? _versionChange;
        int? _maxPackageUpdates;

        protected override UpdateCommand MakeCommand()
        {
            return new UpdateCommand(_localEngine, _logger, _fileSettings);
        }

        protected override void ConfigureCommand(UpdateCommand command)
        {
            if (_versionChange != null) command.AllowedChange = _versionChange;
            if (_maxPackageUpdates != null) command.MaxPackageUpdates = _maxPackageUpdates;
        }

        [TearDown]
        public void Teardown()
        {
            _versionChange = null;
            _maxPackageUpdates = null;
        }

        [Test]
        public async Task ShouldCallEngineAndSucceed()
        {
            _fileSettings.GetSettings().Returns(FileSettings.Empty());
            var command = MakeCommand();

            var status = await command.OnExecute();

            Assert.That(status, Is.EqualTo(0));
            await _localEngine
                .Received(1)
                .Run(Arg.Any<SettingsContainer>(), true);
        }

        [Test]
        public async Task EmptyFileResultsInDefaultSettings()
        {
            var fileSettings = FileSettings.Empty();

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.UserSettings, Is.Not.Null);
            Assert.That(settings.BranchSettings, Is.Not.Null);

            Assert.That(settings.PackageFilters.MinimumAge, Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(settings.PackageFilters.Excludes, Is.Null);
            Assert.That(settings.PackageFilters.Includes, Is.Null);
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(1));

            Assert.That(settings.UserSettings.AllowedChange, Is.EqualTo(VersionChange.Major));
            Assert.That(settings.UserSettings.NuGetSources, Is.Null);
            Assert.That(settings.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.Console));
            Assert.That(settings.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Text));
            Assert.That(settings.UserSettings.CommitMessageTemplate, Is.Null);
            Assert.That(settings.UserSettings.Context, Is.Empty);

            Assert.That(settings.BranchSettings.BranchNameTemplate, Is.Null);
        }

        [Test]
        public async Task WillReadMaxAgeFromFile()
        {
            var fileSettings = new FileSettings
            {
                Age = "8d"
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.PackageFilters.MinimumAge, Is.EqualTo(TimeSpan.FromDays(8)));
        }

        [Test]
        public async Task WillReadIncludeExcludeFromFile()
        {
            var fileSettings = new FileSettings
            {
                Include = "foo",
                Exclude = "bar"
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.PackageFilters.Includes.ToString(), Is.EqualTo("foo"));
            Assert.That(settings.PackageFilters.Excludes.ToString(), Is.EqualTo("bar"));
        }

        [Test]
        public async Task WillReadVersionChangeFromCommandLineOverFile()
        {
            var fileSettings = new FileSettings
            {
                Change = VersionChange.Patch
            };
            _versionChange = VersionChange.Minor;

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings, Is.Not.Null);
            Assert.That(settings.UserSettings.AllowedChange, Is.EqualTo(VersionChange.Minor));
        }

        [Test]
        public async Task WillReadVersionChangeFromFile()
        {
            var fileSettings = new FileSettings
            {
                Change = VersionChange.Patch
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings, Is.Not.Null);
            Assert.That(settings.UserSettings.AllowedChange, Is.EqualTo(VersionChange.Patch));
        }

        [Test]
        public async Task WillReadMaxPackageUpdatesFromFile()
        {
            var fileSettings = new FileSettings
            {
                MaxPackageUpdates = 1234
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(1234));
        }

        [Test]
        public async Task WillReadMaxPackageUpdatesFromCommandLineOverFile()
        {
            var fileSettings = new FileSettings
            {
                MaxPackageUpdates = 123
            };
            _maxPackageUpdates = 23456;

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.PackageFilters, Is.Not.Null);
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(23456));
        }

        [Test]
        public async Task WillReadBranchNameTemplateFromCommandLineOverFile()
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
    }
}
