using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Migration;

namespace NSchema.Commands.Plan;

internal static class PlanOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString);

    public static readonly OptionBinding<string> SchemaDirectory = OptionBinding.Create<string>()
        .FromOption("--schema-dir")
        .WithDescription("Directory containing the desired-schema files to plan from. Required unless set in config.");

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the plan to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy> Destructive = OptionBinding.Create<DestructiveActionPolicy>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy when the plan contains destructive actions: Error (default), Warn, or Allow.");

    public static IEnumerable<Option> All =>
    [
        SchemaDirectory.Option,
        Scope.Option,
        Destructive.Option,
    ];
}
