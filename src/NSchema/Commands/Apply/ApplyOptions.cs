using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Migration;

namespace NSchema.Commands.Apply;

internal static class ApplyOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString);

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the migration to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy> Destructive = OptionBinding.Create<DestructiveActionPolicy>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, or Allow.");

    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve")
        .WithDescription("Skip the interactive confirmation prompt and apply the plan immediately.");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Destructive.Option,
        AutoApprove.Option,
    ];
}
