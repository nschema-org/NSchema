using FluentValidation;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Cli.Configuration.State;

namespace NSchema.Cli.Commands.Plan;

internal sealed class PlanConfigurationValidator : AbstractValidator<PlanConfiguration>
{
    public PlanConfigurationValidator()
    {
        RuleFor(x => x.Schema).SetValidator(new SchemaConfigValidator());
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());

        // A plan diffs the desired schema against a current one, which comes from a live provider or a state store.
        RuleFor(x => x)
            .Must(c => c.Provider.ConfiguredSectionCount >= 1 || c.State.ConfiguredSectionCount >= 1)
            .WithMessage("Plan needs a current schema source: configure a database provider (live) or a state store (offline).");
    }
}
