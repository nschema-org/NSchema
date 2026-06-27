using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Lock.Release;

internal sealed class LockReleaseConfigurationValidator : AbstractValidator<LockReleaseConfiguration>
{
    public LockReleaseConfigurationValidator()
    {
        // The lock lives with the state store, so a configured store is the only thing release needs.
        RuleFor(x => x.State)
            .NotNull()
            .WithMessage("A state store is required for lock release: the lock is held there.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());

        // Safe by default: you must name the lock you mean to release. --force is the explicit opt-out for when you
        // can't read the id first (e.g. clearing a stale lock in CI). Naming an id takes precedence — a redundant
        // --force alongside it is simply ignored, not an error.
        RuleFor(x => x)
            .Must(x => x.LockId is not null || x.Force)
            .WithMessage("lock release needs the id of the lock to release (from 'nschema lock status'), or --force to release whatever lock is held.");
    }
}
