using System.CommandLine;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

internal static class CommonOptions
{
    public static readonly Option<string> Config = new("--config")
    {
        Description = "Path to the NSchema config file. Defaults to ./nschema.json if present.",
    };

    public static readonly Option<bool> NoColor = new("--no-color")
    {
        Description = "Disable colored output. Also honored via the NO_COLOR environment variable.",
        Recursive = true,
    };

    public static readonly Option<string[]> Scope = new("--scope")
    {
        Description = "Limit the migration to specific database schemas (namespaces). May be specified multiple times.",
        AllowMultipleArgumentsPerToken = true,
    };

    public static readonly Option<DestructiveActionPolicy> Destructive = new("--destructive-actions")
    {
        Description = "Policy for destructive actions: Error (default), Warn, or Allow.",
    };
}
