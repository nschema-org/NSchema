using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.State.Pull;

internal sealed class StatePullConfigurationValidator : AbstractValidator<StatePullConfiguration>
{
    public StatePullConfigurationValidator()
    {
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required to pull the recorded state. Add a BACKEND file or BACKEND s3 block to a .sql file.");
        RuleFor(x => x.State!).SetValidator(new StateConfigurationValidator());
    }
}
