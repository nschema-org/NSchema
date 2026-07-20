using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Apply;

internal sealed class ApplyConfigurationValidator : AbstractValidator<ApplyConfiguration>
{
    public ApplyConfigurationValidator()
    {
        // Apply writes to a live database, so a provider is mandatory.
        RuleFor(x => x.Provider)
            .NotNull()
            .WithMessage("A database provider is required for apply.");

        // Apply records what it ran (the snapshot and the run-once ledger), so a state store is mandatory —
        // unless --ephemeral stands one in for the run.
        RuleFor(x => x.State)
            .NotNull()
            .When(x => !x.Ephemeral)
            .WithMessage("A state store is required for apply: the applied schema and script ledger are recorded there. Declare a STATE statement in a configuration (*.env.sql) file, or pass --ephemeral.");
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());
    }
}
