using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Show;

internal sealed class ShowConfigurationValidator : AbstractValidator<ShowConfiguration>
{
    public ShowConfigurationValidator()
    {
        // Show reads the recorded state and never contacts the live database, so a state store is the only source
        // and is mandatory.
        RuleFor(x => x.State.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage("A state store is required for show: the recorded schema is read from there. Add a BACKEND file or BACKEND s3 block to a .sql file.");
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
