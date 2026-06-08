using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Diff.Policies;

namespace NSchema.Commands.Plan;

internal static class PlanOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString);

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the plan to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy> Destructive = OptionBinding.Create<DestructiveActionPolicy>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, or Allow.");

    public static readonly OptionBinding<bool> Destroy = OptionBinding.Create<bool>()
        .FromOption("--destroy")
        .WithDescription("Preview the plan that \"destroy\" would run to tear the managed schema down, without applying it.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        Destroy.Option,
    ];
}
