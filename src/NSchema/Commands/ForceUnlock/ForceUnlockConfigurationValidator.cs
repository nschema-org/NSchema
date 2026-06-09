using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.ForceUnlock;

internal sealed class ForceUnlockConfigurationValidator : AbstractValidator<ForceUnlockConfiguration>
{
    public ForceUnlockConfigurationValidator()
    {
        // The lock lives with the state store, so a configured store is the only thing force-unlock needs.
        RuleFor(x => x.State.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage("A state store is required for force-unlock: the lock is held there. Configure state.file or state.s3 in nschema.json.");
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
