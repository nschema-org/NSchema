using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Refresh;

internal static class RefreshOptions
{
    /// <summary>
    /// Environment-only: the live database is defined in nschema.json (<c>provider.postgres</c>), with the secret
    /// connection string supplied here so it need not be committed. Not a CLI option.
    /// </summary>
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString);
}
