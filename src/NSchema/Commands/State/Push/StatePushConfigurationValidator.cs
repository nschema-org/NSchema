using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.State.Push;

internal sealed class StatePushConfigurationValidator : AbstractValidator<StatePushConfiguration>
{
    public StatePushConfigurationValidator()
    {
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required to push state. Add a BACKEND file or BACKEND s3 block to a .sql file.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
