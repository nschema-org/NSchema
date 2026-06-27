using FluentValidation;
using NSchema.Configuration.State;

namespace NSchema.Commands.Plan;

internal sealed class PlanConfigurationValidator : AbstractValidator<PlanConfiguration>
{
    public PlanConfigurationValidator()
    {
        RuleFor(x => x.State!).SetValidator(new StateConfigValidator());

        // A forward plan diffs the desired schema (the *.sql files under the working directory) against a current
        // one, so it needs a current-schema source — a live provider or a state store.
        When(x => !x.Destroy, () =>
        {
            RuleFor(x => x)
                .Must(c => c.Provider is not null || c.State is not null)
                .WithMessage("Plan needs a current schema source: configure a database provider (live) or a state store (offline).");
        });

        // --destroy previews the same teardown "destroy" runs: the SQL is rendered against the provider, so a provider
        // is mandatory. The managed schema comes from the state store, or falls back to the working-directory schema.
        When(x => x.Destroy, () =>
        {
            RuleFor(x => x.Provider)
                .NotNull()
                .WithMessage("A database provider is required for plan --destroy: the teardown SQL is rendered against it.");
        });
    }
}
