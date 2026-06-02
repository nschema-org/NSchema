using System.CommandLine;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

/// <summary>
/// The shared set of command-line options.
/// </summary>
internal static class CliOptions
{
    public static readonly Option<string?> Config = new("--config")
    {
        Description = "Path to the NSchema config file. Defaults to ./nschema.json if present.",
        Recursive = true,
    };

    public static readonly Option<string?> ConnectionString = new("--connection-string")
    {
        Description = "Connection string for the target database. Overrides the config file.",
        Recursive = true,
    };

    public static readonly Option<string?> Provider = new("--provider")
    {
        Description = "Name of the bundled database provider to use (e.g. postgres).",
        Recursive = true,
    };

    public static readonly Option<string[]> Scope = new("--scope")
    {
        Description = "Limit the migration to specific database schemas (namespaces). May be specified multiple times.",
        Recursive = true,
        AllowMultipleArgumentsPerToken = true,
    };

    public static readonly Option<SchemaFormat> Format = new("--format")
    {
        Description = "The format the desired schema is expressed in: yaml (default) or json.",
    };

    public static readonly Option<string?> SchemaDir = new("--schema-dir")
    {
        Description = "Directory containing the desired-schema files. Required for plan and apply unless set in config.",
    };

    public static readonly Option<string?> SchemaGlob = new("--schema-glob")
    {
        Description = "Glob matched within the schema directory. Defaults to a per-format pattern (e.g. **/*.yaml).",
    };

    public static readonly Option<DestructiveActionPolicy?> Destructive = new("--destructive-actions")
    {
        Description = "Policy for destructive actions: Error (default), Warn, or Allow.",
        Recursive = true,
    };

    public static readonly Option<string?> StateFile = new("--state-file")
    {
        Description = "Path to a local state file enabling offline planning and post-apply state capture.",
        Recursive = true,
    };

    public static readonly Option<bool> AutoApprove = new("--auto-approve")
    {
        Description = "Skip the interactive confirmation prompt and apply the plan immediately.",
    };
}
