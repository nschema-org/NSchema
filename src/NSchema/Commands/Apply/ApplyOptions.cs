using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Diff.Policies;
using NSchema.Policies;

namespace NSchema.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope", "-s")
        .AllowMultipleArguments()
        .WithDescription("Limit the migration to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy?> Destructive = OptionBinding.Create<DestructiveActionPolicy?>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, or Allow.");

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

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        DataHazards.Option,
        AutoApprove.Option,
        PlanFile.Option,
        NoLock.Option,
    ];
}
