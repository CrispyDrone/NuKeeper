using NSubstitute;
using NUnit.Framework;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.Output;
using NuKeeper.Commands;
using System.Threading.Tasks;
using System;

namespace NuKeeper.Tests.Commands
{
    [TestFixture]
#pragma warning disable CA1812
    class InspectCommandTests : CommandTestsBase<InspectCommand>
    {
        OutputDestination? _outputDestination;
        OutputFormat? _outputFormat;
        string _outputFileName;

        protected override InspectCommand MakeCommand()
        {
            return new InspectCommand(_localEngine, _logger, _fileSettings);
        }

        protected override void ConfigureCommand(InspectCommand command)
        {
            if (_outputDestination != null) command.OutputDestination = _outputDestination;
            if (_outputFormat != null) command.OutputFormat = _outputFormat;
            if (_outputFileName != null) command.OutputFileName = _outputFileName;
        }

        [TearDown]
        public void TearDown()
        {
            _outputDestination = null;
            _outputFormat = null;
            _outputFileName = null;
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
                .Run(Arg.Any<SettingsContainer>(), false);
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
            Assert.That(settings.PackageFilters.MaxPackageUpdates, Is.EqualTo(0));

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
        public async Task InvalidMaxAgeWillFail()
        {
            var fileSettings = new FileSettings
            {
                Age = "fish"
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Null);
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
        public async Task LogLevelIsNormalByDefault()
        {
            _fileSettings.GetSettings().Returns(FileSettings.Empty());
            var command = MakeCommand();

            await command.OnExecute();

            _logger
                .Received(1)
                .Initialise(LogLevel.Normal, LogDestination.Console, Arg.Any<string>());
        }

        [Test]
        public async Task ShouldSetLogLevelFromCommand()
        {
            _fileSettings.GetSettings().Returns(FileSettings.Empty());
            var command = MakeCommand();
            command.Verbosity = LogLevel.Minimal;

            await command.OnExecute();

            _logger
                .Received(1)
                .Initialise(LogLevel.Minimal, LogDestination.Console, Arg.Any<string>());
        }

        [Test]
        public async Task ShouldSetLogLevelFromFile()
        {
            var settings = new FileSettings
            {
                Verbosity = LogLevel.Detailed
            };
            _fileSettings.GetSettings().Returns(settings);
            var command = MakeCommand();

            await command.OnExecute();

            _logger
                .Received(1)
                .Initialise(LogLevel.Detailed, LogDestination.Console, Arg.Any<string>());
        }

        [Test]
        public async Task CommandLineLogLevelOverridesFile()
        {
            var settings = new FileSettings
            {
                Verbosity = LogLevel.Detailed
            };
            _fileSettings.GetSettings().Returns(settings);
            var command = MakeCommand();
            command.Verbosity = LogLevel.Minimal;

            await command.OnExecute();

            _logger
                .Received(1)
                .Initialise(LogLevel.Minimal, LogDestination.Console, Arg.Any<string>());
        }

        [Test]
        public async Task LogToFileBySettingFileName()
        {
            var settings = FileSettings.Empty();
            _fileSettings.GetSettings().Returns(settings);
            var command = MakeCommand();
            command.LogFile = "somefile.log";

            await command.OnExecute();

            _logger
                .Received(1)
                .Initialise(LogLevel.Normal, LogDestination.File, "somefile.log");
        }

        [Test]
        public async Task LogToFileBySettingLogDestination()
        {
            var settings = FileSettings.Empty();
            _fileSettings.GetSettings().Returns(settings);
            var command = MakeCommand();
            command.LogDestination = LogDestination.File;

            await command.OnExecute();

            _logger
                .Received(1)
                .Initialise(LogLevel.Normal, LogDestination.File, "nukeeper.log");
        }

        [Test]
        public async Task ShouldSetOutputOptionsFromFile()
        {
            var fileSettings = new FileSettings
            {
                OutputDestination = OutputDestination.File,
                OutputFormat = OutputFormat.Csv,
                OutputFileName = "foo.csv"
            };

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.File));
            Assert.That(settings.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Csv));
            Assert.That(settings.UserSettings.OutputFileName, Is.EqualTo("foo.csv"));
        }

        [Test]
        public async Task WhenFileNameIsExplicit_ShouldDefaultOutputDestToFile()
        {
            var fileSettings = new FileSettings
            {
                OutputDestination = null,
                OutputFormat = OutputFormat.Csv
            };
            _outputFileName = "foo.csv";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.File));
            Assert.That(settings.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Csv));
            Assert.That(settings.UserSettings.OutputFileName, Is.EqualTo("foo.csv"));
        }

        [Test]
        public async Task WhenFileNameIsExplicit_ShouldKeepOutputDest()
        {
            var fileSettings = new FileSettings
            {
                OutputDestination = OutputDestination.Off,
                OutputFormat = OutputFormat.Csv
            };
            _outputFileName = "foo.csv";

            var (settings, _) = await CaptureSettings(fileSettings);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.Off));
            Assert.That(settings.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Csv));
            Assert.That(settings.UserSettings.OutputFileName, Is.EqualTo("foo.csv"));
        }

        [Test]
        public async Task ShouldSetOutputOptionsFromCommand()
        {
            _outputDestination = OutputDestination.File;
            _outputFormat = OutputFormat.Csv;

            var (settingsOut, _) = await CaptureSettings(FileSettings.Empty());

            Assert.That(settingsOut.UserSettings.OutputDestination, Is.EqualTo(OutputDestination.File));
            Assert.That(settingsOut.UserSettings.OutputFormat, Is.EqualTo(OutputFormat.Csv));
        }
    }
}
