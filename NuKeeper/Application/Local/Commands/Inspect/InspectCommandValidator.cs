using FluentValidation;

namespace NuKeeper.Application.Local.Commands.Inspect
{
    class InspectCommandValidator : AbstractValidator<InspectCommand>
    {
        public InspectCommandValidator()
        {
            RuleFor(c => c.Path)
                .NotEmpty();

            RuleFor(c => c.OutputDestination)
                .IsInEnum();

            RuleFor(c => c.OutputFormat)
                .IsInEnum();

            RuleFor(c => c.AllowedChange)
                .IsInEnum();

            RuleFor(c => c.UsePrerelease)
                .IsInEnum();

            Transform(
                c => c.MinimumPackageAge,
                age =>
                {
                    if (age == null) return (int?)null;

                    if (!int.TryParse(age, out int ageAsInt))
                        ageAsInt = -1;

                    return ageAsInt;
                }
            ).GreaterThanOrEqualTo(0);

            RuleFor(c => c.LogDestination)
                .IsInEnum();

            RuleFor(c => c.Verbosity)
                .IsInEnum();
        }
    }
}
