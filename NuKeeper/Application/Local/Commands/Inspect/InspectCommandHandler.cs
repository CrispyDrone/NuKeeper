using NuGet.Common;
using NuGet.Configuration;
using NuGet.Credentials;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.NuGet;
using NuKeeper.Abstractions.Output;
using NuKeeper.Abstractions.RepositoryInspection;
using NuKeeper.Inspection;
using NuKeeper.Inspection.Files;
using NuKeeper.Inspection.Report;
using NuKeeper.Inspection.Sort;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NuKeeper.Application.Local.Commands.Inspect
{
    class InspectCommandHandler
    {
        private readonly ILogger _nugetLogger;
        private readonly INuKeeperLogger _logger;
        private readonly IUpdateFinder _updateFinder;
        private readonly IPackageUpdateSetSort _sorter;
        private readonly IReporter _reporter;

        public InspectCommandHandler(
            ILogger nugetLogger,
            INuKeeperLogger logger,
            IUpdateFinder updateFinder,
            IPackageUpdateSetSort sorter,
            IReporter reporter
        )
        {
            _nugetLogger = nugetLogger;
            _logger = logger;
            _updateFinder = updateFinder;
            _sorter = sorter;
            _reporter = reporter;
        }

        public async Task<IReadOnlyCollection<PackageUpdateSet>> Handle(InspectCommand command, CancellationToken cancellationToken)
        {
            DefaultCredentialServiceUtility.SetupDefaultCredentialService(_nugetLogger, true);

            var dir = command.Path;

            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = Directory.GetCurrentDirectory();
            }

            var folder = new Folder(_logger, new DirectoryInfo(dir));

            var sources = command.NuGetSources;

            if (sources == null)
            {
                sources = ReadNugetSources(folder);
            }

            if (sources == null)
            {
                sources = NuGetSources.GlobalFeed;
            }

            var updates = await _updateFinder.FindPackageUpdateSets(
                folder,
                sources,
                command.AllowedChange ?? VersionChange.Major,
                command.UsePrerelease ?? UsePrerelease.FromPrerelease,
                new Regex(command.Include),
                new Regex(command.Exclude)
            );

            var sortedUpdates = _sorter.Sort(updates)
                .ToList();

            Report(
                command.OutputDestination
                    ?? (string.IsNullOrWhiteSpace(command.OutputFileName)
                        ? OutputDestination.Console
                        : OutputDestination.File
                ),
                command.OutputFormat ?? OutputFormat.Text,
                command.OutputFileName ?? "nukeeper.out",
                sortedUpdates
            );

            return sortedUpdates;
        }

        private NuGetSources ReadNugetSources(Folder folder)
        {
            var settings = Settings.LoadDefaultSettings(folder.FullPath);

            foreach (var file in settings.GetConfigFilePaths())
            {
                _logger.Detailed($"Reading file {file} for package sources");
            }

            var enabledSources = SettingsUtility.GetEnabledSources(settings).ToList();

            foreach (var source in enabledSources)
            {
                _logger.Detailed(
                    $"Read [{source.Name}] : {source.SourceUri} from file: {source.Source}");
            }

            return new NuGetSources(enabledSources);
        }

        private void Report(
            OutputDestination outputDestination,
            OutputFormat outputFormat,
            string outputFileName,
            IReadOnlyCollection<PackageUpdateSet> updates
        )
        {
            _reporter.Report(
                outputDestination,
                outputFormat,
                "Inspect",
                outputFileName,
                updates
            );
        }
    }
}
