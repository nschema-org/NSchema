using FluentValidation;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Configuration.Provider;

internal sealed class ProviderConfigValidator : AbstractValidator<ProviderConfig>
{
    public ProviderConfigValidator()
    {
        RuleFor(x => x)
            .Must(HaveOnlyOneConfiguration)
            .WithMessage("More than one database provider is configured; specify exactly one.");

        RuleFor(x => x.Postgres)
            .SetNonNullableValidator(new PostgresProviderConfigValidator());
    }

    private static bool HaveOnlyOneConfiguration(ProviderConfig config) => new []
    {
        config.Postgres is not null,
    }.Count(x => x) <= 1;
}
