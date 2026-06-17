using FluentValidation;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Refresh;

internal sealed class RefreshConfigurationValidator : AbstractValidator<RefreshConfiguration>
{
    public RefreshConfigurationValidator()
    {
        // Refresh reads the live schema, so a provider is mandatory.
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage($"A database provider is required for refresh.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        // Refresh writes the snapshot to the state store, so a store is mandatory.
        RuleFor(x => x.State.ConfiguredSectionCount)
            .Equal(1)
            .WithMessage("A state store is required for refresh: the live schema is written there. Configure --state-file or --state-s3-bucket/--state-s3-key.");
        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
