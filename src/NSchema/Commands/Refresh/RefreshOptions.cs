using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Refresh;

internal static class RefreshOptions
{
    /// <summary>
    /// The connection string for the Postgres database.
    /// </summary>
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString)
        .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

    public static readonly OptionBinding<int?> CommandTimeout = OptionBinding.Create<int?>()
        .FromProjectConfig(c => c.Provider?.Postgres?.CommandTimeout);

    public static readonly OptionBinding<StateConfig> State = OptionBinding.Create<StateConfig>()
        .FromProjectConfig(c => c.State);
}
