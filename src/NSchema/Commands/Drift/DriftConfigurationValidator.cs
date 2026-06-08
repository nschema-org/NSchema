using FluentValidation;
using NSchema.Configuration;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Drift;

internal sealed class DriftConfigurationValidator : AbstractValidator<DriftConfiguration>
{
    public DriftConfigurationValidator()
    {
        // Drift reads the live schema to compare against the recorded state, so a provider is mandatory.
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage($"A database provider is required for drift. Configure provider.postgres in nschema.json, or set {EnvironmentVariables.PostgresConnectionString}.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        // Drift compares the live schema against the recorded state, so a state store is mandatory.
        RuleFor(x => x.State.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage("A state store is required for drift: the live schema is compared against the recorded state there. Configure state.file or state.s3 in nschema.json.");
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
