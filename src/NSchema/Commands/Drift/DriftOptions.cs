using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Drift;

internal static class DriftOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString)
        .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

    public static readonly OptionBinding<int?> PostgresCommandTimeout = OptionBinding.Create<int?>()
        .FromProjectConfig(c => c.Provider?.Postgres?.CommandTimeout);

    public static readonly OptionBinding<StateConfig> State = OptionBinding.Create<StateConfig>()
        .FromProjectConfig(c => c.State);

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the drift check to specific database schemas (namespaces). May be specified multiple times.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
    ];
}
