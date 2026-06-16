using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

internal static class DestroyOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString)
        .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

    public static readonly OptionBinding<int?> CommandTimeout = OptionBinding.Create<int?>()
        .FromProjectConfig(c => c.Provider?.Postgres?.CommandTimeout);

    public static readonly OptionBinding<StateConfig> State = OptionBinding.Create<StateConfig>()
        .FromProjectConfig(c => c.State);

    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve")
        .WithDescription("Skip the interactive confirmation prompt and tear down the schema immediately.");

    public static IEnumerable<Option> All =>
    [
        AutoApprove.Option,
    ];
}
