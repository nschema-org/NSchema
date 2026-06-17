using FluentValidation;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Apply;

internal sealed class ApplyConfigurationValidator : AbstractValidator<ApplyConfiguration>
{
    public ApplyConfigurationValidator()
    {
        // Apply writes to a live database, so a provider is mandatory — not merely "at most one".
        RuleFor(x => x.Provider.ConfiguredSectionCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage($"A database provider is required for apply.");
        RuleFor(x => x.Provider).SetValidator(new ProviderConfigValidator());

        RuleFor(x => x.State).SetValidator(new StateConfigValidator());
    }
}
