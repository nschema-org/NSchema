using FluentValidation;

namespace NSchema.Configuration.Provider;

internal sealed class SqlServerProviderConfigValidator : AbstractValidator<SqlServerProviderConfig>
{
    public SqlServerProviderConfigValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .WithMessage($"provider.sqlserver.connectionString is required. Set it using {EnvironmentVariables.SqlServerConnectionString}.");

        RuleFor(x => x.CommandTimeout)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CommandTimeout.HasValue)
            .WithMessage("provider.sqlserver.commandTimeout must not be negative.");
    }
}
