using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.LockStatus;

internal sealed class LockStatusConfigurationValidator : AbstractValidator<LockStatusConfiguration>
{
    public LockStatusConfigurationValidator()
    {
        // The lock lives with the state store, so without one there is nothing to inspect.
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required for lock-status: the lock is held there.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
