using NuGet.Configuration;
using NuGet.Versioning;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.NuGet;
using NuKeeper.Abstractions.RepositoryInspection;
using NuKeeper.Update.ProcessRunner;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NuKeeper.Update.Process
{
    public class NuGetUpdatePackageCommand : INuGetUpdatePackageCommand
    {
        private readonly IExternalProcess _externalProcess;
        private readonly INuKeeperLogger _logger;
        private readonly INuGetPath _nuGetPath;
        private readonly IMonoExecutor _monoExecutor;

        public NuGetUpdatePackageCommand(
            INuKeeperLogger logger,
            INuGetPath nuGetPath,
            IMonoExecutor monoExecutor,
            IExternalProcess externalProcess)
        {
            _logger = logger;
            _nuGetPath = nuGetPath;
            _monoExecutor = monoExecutor;
            _externalProcess = externalProcess;
        }

        public async Task Invoke(PackageInProject currentPackage,
            NuGetVersion newVersion, PackageSource packageSource, NuGetSources allSources)
        {
            if (currentPackage == null)
            {
                throw new ArgumentNullException(nameof(currentPackage));
            }

            if (allSources == null)
            {
                throw new ArgumentNullException(nameof(allSources));
            }

            var projectPath = currentPackage.Path.Info.DirectoryName;

            var nuget = _nuGetPath.Executable;
            if (string.IsNullOrWhiteSpace(nuget))
            {
                _logger.Normal("Cannot find NuGet.exe for package update");
                return;
            }

            var sources = allSources.CommandLine("-Source");
            /* todo: Make DependencyVersion configurable?
               note: `Lowest` for now because:
                   1. If maxpackageupdates = 1, we still want to generate a working update, so we can't state `Ignore`
                   2. Not all packages practice semantic versioning, so we can't utilize any of the "within same major version" options
               todo: update should take into account only packages for the correct target framework!! It's currently completely ignoring this
            */
            var updateCommand = $"update packages.config -Id {currentPackage.Id} -Version {newVersion} {sources} -NonInteractive -DependencyVersion Lowest";

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (await _monoExecutor.CanRun())
                {
                    await _monoExecutor.Run(projectPath, nuget, updateCommand, true);
                }
                else
                {
                    _logger.Error("Cannot run NuGet.exe. It requires either Windows OS Platform or Mono installation");
                }
            }
            else
            {
                await _externalProcess.Run(projectPath, nuget, updateCommand, true);
            }
        }
    }
}
