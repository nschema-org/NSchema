using FluentValidation;

namespace NSchema.Configuration.Provider;

internal sealed class SqliteProviderConfigValidator : AbstractValidator<SqliteProviderConfig>
{
    public SqliteProviderConfigValidator()
    {
        RuleFor(x => x.ConnectionString)
            .NotEmpty()
            .WithMessage($"provider.sqlite.connectionString is required. Set it using {EnvironmentVariables.SqliteConnectionString}.");
    }
}
