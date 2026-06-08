using FluentValidation;

namespace NSchema.Configuration.Provider;

internal sealed class PostgresProviderConfigValidator : AbstractValidator<PostgresProviderConfig>
{
    public PostgresProviderConfigValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .WithMessage($"provider.postgres.connectionString is required. Set it using {EnvironmentVariables.PostgresConnectionString}.");

        RuleFor(x => x.CommandTimeout)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CommandTimeout.HasValue)
            .WithMessage("provider.postgres.commandTimeout must not be negative.");
    }
}
