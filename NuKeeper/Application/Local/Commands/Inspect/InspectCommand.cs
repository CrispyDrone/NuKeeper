using McMaster.Extensions.CommandLineUtils;
using NuKeeper.Abstractions.Configuration;
using NuKeeper.Abstractions.Logging;
using NuKeeper.Abstractions.NuGet;
using NuKeeper.Abstractions.Output;
using System.Threading;
using System.Threading.Tasks;

namespace NuKeeper.Application.Local.Commands.Inspect
{
    // TODO: Set up pipeline (perhaps use mediatr?)
    // TODO: How to do configuration? Multiple sources (commandline, files, defaults, custom?). Needs to be flexible. Needs to be clear which values are used in which flows, so no "one settingscontainer to rule them all".
    // TODO: Separate "client commands" from "application commands"? What could be different between them?
    // TODO: Report inside or outside of handler?
    // TODO: Apply principle of reused abstractions. So first, copy all code into this handler, do the same thing for multiple handers, and then find the hidden abstractions.
    [Command("inspect", "i", Description = "Checks projects existing locally for possible updates.")]
    sealed class InspectCommand
    {
        private readonly InspectCommandValidator _validator;
        private readonly InspectCommandHandler _handler;

        public InspectCommand(InspectCommandValidator validator, InspectCommandHandler handler)
        {
            _validator = validator;
            _handler = handler;
        }

        public async Task<int> OnExecute()
        {
            var validationResult = _validator.Validate(this);
            if (!validationResult.IsValid)
                return -1;

            var result = await _handler.Handle(this, CancellationToken.None);
            return 0;
        }

        [Argument(0, Description = "The path to a .sln or project file, or to a directory containing a .NET solution/project. " +
               "If none is specified, the current directory will be used.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        // ReSharper disable once MemberCanBePrivate.Global
        public string Path { get; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "outputformat",
            Description = "Format for output.")]
        public OutputFormat? OutputFormat { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "outputdestination",
            Description = "Destination for output.")]
        public OutputDestination? OutputDestination { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "outputfile",
            Description = "File name for output.")]
        public string OutputFileName { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "c", LongName = "change",
            Description = "Allowed version change: Patch, Minor, Major. Defaults to Major.")]
        public VersionChange? AllowedChange { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "useprerelease",
            Description = "Allowed prerelease: Always, Never, FromPrerelease. Defaults to FromPrerelease.")]
        public UsePrerelease? UsePrerelease { get; set; }

        [Option(CommandOptionType.MultipleValue, ShortName = "s", LongName = "source",
            Description =
                "Specifies a NuGet package source to use during the operation. This setting overrides all of the sources specified in the NuGet.config files. Multiple sources can be provided by specifying this option multiple times.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        // ReSharper disable once MemberCanBePrivate.Global
        public string[] Source { get; }

        public NuGetSources NuGetSources => Source == null ? null : new NuGetSources(Source);

        [Option(CommandOptionType.SingleValue, ShortName = "a", LongName = "age",
            Description = "Exclude updates that do not meet a minimum age, in order to not consume packages immediately after they are released. Examples: 0 = zero, 12h = 12 hours, 3d = 3 days, 2w = two weeks. The default is 7 days.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        // ReSharper disable once MemberCanBePrivate.Global
        public string MinimumPackageAge { get; }

        [Option(CommandOptionType.SingleValue, ShortName = "i", LongName = "include",
            Description = "Only consider packages matching this regex pattern.")]
        public string Include { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "e", LongName = "exclude",
            Description = "Do not consider packages matching this regex pattern.")]
        public string Exclude { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "v", LongName = "verbosity",
            Description = "Sets the verbosity level of the command. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed].")]
        public LogLevel? Verbosity { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "logdestination",
            Description = "Destination for logging.")]
        public LogDestination? LogDestination { get; set; }

        [Option(CommandOptionType.SingleValue, ShortName = "", LongName = "logfile",
            Description = "Log to the named file.")]
        public string LogFile { get; set; }
    }
}
