using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Plan;

internal static class PlanOptions
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
        .WithDescription("Limit the plan to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy?> Destructive = OptionBinding.Create<DestructiveActionPolicy?>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .FromProjectConfig(c => c.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, or Allow.");

    public static readonly OptionBinding<bool> Destroy = OptionBinding.Create<bool>()
        .FromOption("--destroy")
        .WithDescription("Preview the plan that \"destroy\" would run to tear the managed schema down, without applying it.");

    public static readonly OptionBinding<string> Out = OptionBinding.Create<string>()
        .FromOption("--out")
        .WithDescription("Write the computed plan to this file so it can be replayed later with apply --plan-file.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        Destroy.Option,
        Out.Option,
    ];
}
