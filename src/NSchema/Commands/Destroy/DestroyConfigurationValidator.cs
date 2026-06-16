using FluentValidation;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

internal sealed class DestroyConfigurationValidator : AbstractValidator<DestroyConfiguration>
{
    public DestroyConfigurationValidator()
    {
        // Destroy generates and executes SQL against a live database, so a provider is mandatory.
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage($"A database provider is required for destroy.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
