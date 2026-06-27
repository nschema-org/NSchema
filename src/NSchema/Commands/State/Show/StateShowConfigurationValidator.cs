using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.State.Show;

internal sealed class StateShowConfigurationValidator : AbstractValidator<StateShowConfiguration>
{
    public StateShowConfigurationValidator()
    {
        // Without an explicit file, the recorded state comes from the configured store, so one is mandatory.
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required to show the recorded state. Add a BACKEND file or BACKEND s3 block to a .sql file, or pass a state file path to show it directly.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
