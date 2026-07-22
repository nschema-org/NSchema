using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Lock.Acquire;

internal sealed class LockAcquireConfigurationValidator : AbstractValidator<LockAcquireConfiguration>
{
    public LockAcquireConfigurationValidator()
    {
        // The lock lives with the state store, so without one there is nothing to acquire.
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required for lock acquire: the lock is held there.");
        RuleFor(x => x.State!).SetValidator(new StateConfigurationValidator());

        RuleFor(x => x.Reason).NotEmpty();

        RuleFor(x => x.TtlText)
            .Must(t => t is null || Duration.TryParse(t, out _))
            .WithMessage("--ttl must be a duration like 30m, 2h, 90s, or 1d.");
    }
}
