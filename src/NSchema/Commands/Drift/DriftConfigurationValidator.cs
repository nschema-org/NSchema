using FluentValidation;
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
            .WithMessage($"A database provider is required for drift.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        // Drift compares the live schema against the recorded state, so a state store is mandatory.
        RuleFor(x => x.State.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage("A state store is required for drift: the live schema is compared against the recorded state there.");
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
