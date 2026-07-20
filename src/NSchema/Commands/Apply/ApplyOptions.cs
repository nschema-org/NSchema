using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope", "-s")
        .AllowMultipleArguments()
        .WithDescription("Limit the migration to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<PolicyEnforcement?> Destructive = OptionBinding.Create<PolicyEnforcement?>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, Allow, or Ignore.");

    public static readonly OptionBinding<PolicyEnforcement?> DataHazards = OptionBinding.Create<PolicyEnforcement?>()
        .FromOption("--data-hazards")
        .FromEnvironmentVariable(EnvironmentVariables.DataHazardPolicy)
        .WithDescription("Policy when the plan contains changes that can fail on existing data: Error, Warn (default), Allow, or Ignore.");

    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve", "-y")
        .WithDescription("Skip the interactive confirmation prompt and apply the plan immediately.");

    public static readonly OptionBinding<string> PlanFile = OptionBinding.Create<string>()
        .FromOption("--plan-file", "-p")
        .WithDescription("Apply a plan previously saved with plan --out, replaying exactly that plan instead of computing a fresh one.");

    public static readonly OptionBinding<bool> NoLock = OptionBinding.Create<bool>()
        .FromOption("--no-lock")
        .WithDescription("Apply without acquiring the state lock. You take responsibility for preventing concurrent runs (e.g. when operating under a manually-held lock).");

    public static readonly OptionBinding<bool> Ephemeral = OptionBinding.Create<bool>()
        .FromOption("--ephemeral")
        .WithDescription("Run against an in-memory state store discarded when the command exits, instead of a configured STATE store — for CI runs against disposable databases. Run-once script history does not persist.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        DataHazards.Option,
        AutoApprove.Option,
        PlanFile.Option,
        NoLock.Option,
        Ephemeral.Option,
    ];
}
