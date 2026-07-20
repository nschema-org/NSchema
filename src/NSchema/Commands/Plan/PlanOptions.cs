using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Plan;

internal static class PlanOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope", "-s")
        .AllowMultipleArguments()
        .WithDescription("Limit the plan to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<PolicyEnforcement?> Destructive = OptionBinding.Create<PolicyEnforcement?>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, Allow, or Ignore.");

    public static readonly OptionBinding<PolicyEnforcement?> DataHazards = OptionBinding.Create<PolicyEnforcement?>()
        .FromOption("--data-hazards")
        .FromEnvironmentVariable(EnvironmentVariables.DataHazardPolicy)
        .WithDescription("Policy when the plan contains changes that can fail on existing data: Error, Warn (default), Allow, or Ignore.");

    public static readonly OptionBinding<bool> Destroy = OptionBinding.Create<bool>()
        .FromOption("--destroy")
        .WithDescription("Preview the plan that \"destroy\" would run to tear the managed schema down, without applying it.");

    public static readonly OptionBinding<string> Out = OptionBinding.Create<string>()
        .FromOption("--out", "-o")
        .WithDescription("Write the computed plan to this file so it can be replayed later with apply --plan-file.");

    public static readonly OptionBinding<bool> DetailedExitCode = OptionBinding.Create<bool>()
        .FromOption("--detailed-exitcode")
        .WithDescription("Return a detailed exit code: 0 = no changes, 2 = changes present (errors remain 1). For CI gating.");

    public static readonly OptionBinding<bool> Ephemeral = OptionBinding.Create<bool>()
        .FromOption("--ephemeral")
        .WithDescription("Run against an in-memory state store discarded when the command exits, instead of a configured STATE store — for CI runs against disposable databases. Run-once script history does not persist.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        DataHazards.Option,
        Destroy.Option,
        Out.Option,
        DetailedExitCode.Option,
        Ephemeral.Option,
    ];
}
