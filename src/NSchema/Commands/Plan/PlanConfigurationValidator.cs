using FluentValidation;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Configuration.State;

namespace NSchema.Commands.Plan;

internal sealed class PlanConfigurationValidator : AbstractValidator<PlanConfiguration>
{
    public PlanConfigurationValidator()
    {
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());

        // A forward plan diffs the desired schema against a current one, so it needs a valid desired schema and a
        // current-schema source — a live provider or a state store.
        When(x => !x.Destroy, () =>
        {
            RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator());

            RuleFor(x => x)
                .Must(c => c.Provider.ConfiguredSectionCount >= 1 || c.State.ConfiguredSectionCount >= 1)
                .WithMessage("Plan needs a current schema source: configure a database provider (live) or a state store (offline).");
        });

        // --destroy previews the same teardown "destroy" runs, so it takes the same inputs: a provider to render the
        // SQL against, and a managed-schema source — the state store, or a desired schema to fall back on.
        When(x => x.Destroy, () =>
        {
            RuleFor(x => x.Provider.ConfiguredSectionCount)
                .GreaterThanOrEqualTo(1)
                .WithMessage($"A database provider is required for plan --destroy: the teardown SQL is rendered against it. Configure provider.postgres in nschema.json, or set {EnvironmentVariables.PostgresConnectionString}.");

            RuleFor(x => x)
                .Must(c => c.State.ConfiguredSectionCount >= 1 || c.HasSchema)
                .WithMessage("Plan --destroy needs a managed schema source: configure a state store, or a desired schema via \"schema.dir\" to fall back on.");

            RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator()).When(x => x.HasSchema);
        });
    }
}
