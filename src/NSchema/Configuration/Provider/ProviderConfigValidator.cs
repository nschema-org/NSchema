using FluentValidation;

namespace NSchema.Configuration.Provider;

internal sealed class ProviderConfigValidator : AbstractValidator<ProviderConfig>
{
    public ProviderConfigValidator()
    {
        // The DDL reader already rejects a second PROVIDER block; this guards the resolved model for any other caller.
        RuleFor(x => x.ConfiguredSectionCount)
            .LessThanOrEqualTo(1)
            .WithMessage("More than one database provider is configured; specify exactly one.");
    }
}
