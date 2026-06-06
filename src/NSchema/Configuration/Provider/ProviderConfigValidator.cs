using FluentValidation;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Configuration.Provider;

internal sealed class ProviderConfigValidator : AbstractValidator<ProviderConfig>
{
    public ProviderConfigValidator()
    {
        RuleFor(x => x.ConfiguredSectionCount)
            .LessThanOrEqualTo(1)
            .WithMessage("More than one database provider is configured; specify exactly one.");

        RuleFor(x => x.Postgres)
            .SetNonNullableValidator(new PostgresProviderConfigValidator());
    }
}
