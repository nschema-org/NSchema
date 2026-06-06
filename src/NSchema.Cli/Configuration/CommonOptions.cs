using NSchema.Cli.Configuration.Binding;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

internal static class CommonOptions
{
    public static readonly OptionBinding<string> Config = OptionBinding.Create<string>()
        .FromOption("--config")
        .WithDescription("Path to the NSchema config file. Defaults to ./nschema.json if present.");

    public static readonly OptionBinding<bool> NoColor = OptionBinding.Create<bool>()
        .FromOption("--no-color")
        .FromEnvironmentVariable(EnvironmentVariables.NoColor)
        .Recursive()
        .WithDescription("Disable colored output.");

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the migration to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<DestructiveActionPolicy> Destructive = OptionBinding.Create<DestructiveActionPolicy>()
        .FromOption("--destructive-actions")
        .FromEnvironmentVariable(EnvironmentVariables.DestructiveActionPolicy)
        .WithDescription("Policy for destructive actions: Error (default), Warn, or Allow.");
}
