using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString)
        .FromProjectConfig(c => c.Provider?.Postgres?.ConnectionString);

    public static readonly OptionBinding<int?> CommandTimeout = OptionBinding.Create<int?>()
        .FromProjectConfig(c => c.Provider?.Postgres?.CommandTimeout);

    public static readonly OptionBinding<StateConfig> State = OptionBinding.Create<StateConfig>()
        .FromProjectConfig(c => c.State);

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the migration to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy?> Destructive = OptionBinding.Create<DestructiveActionPolicy?>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .FromProjectConfig(c => c?.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, or Allow.");

    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve")
        .WithDescription("Skip the interactive confirmation prompt and apply the plan immediately.");

    public static readonly OptionBinding<string> PlanFile = OptionBinding.Create<string>()
        .FromOption("--plan-file")
        .WithDescription("Apply a plan previously saved with plan --out, replaying exactly that plan instead of computing a fresh one.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        AutoApprove.Option,
        PlanFile.Option,
    ];
}
