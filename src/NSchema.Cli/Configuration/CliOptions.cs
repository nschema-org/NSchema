using System.CommandLine;
using NSchema.Migration;

namespace NSchema.Cli;

/// <summary>
/// The shared set of command-line options.
/// </summary>
internal static class CliOptions
{
    public static readonly Option<string?> Config = new("--config", "-f")
    {
        Description = "Path to the NSchema config file. Defaults to ./nschema.json if present.",
        Recursive = true,
    };

    public static readonly Option<string?> ConnectionString = new("--connection-string", "-c")
    {
        Description = "Connection string for the target database. Overrides the config file.",
        Recursive = true,
    };

    public static readonly Option<string?> Provider = new("--provider", "-p")
    {
        Description = "Name of the bundled database provider to use (e.g. postgres).",
        Recursive = true,
    };

    public static readonly Option<string[]> Schema = new("--schema", "-s")
    {
        Description = "A desired-schema file path or glob pattern. May be specified multiple times.",
        Recursive = true,
        AllowMultipleArgumentsPerToken = true,
    };

    public static readonly Option<string[]> SchemaName = new("--schema-name")
    {
        Description = "Limit the migration to specific schema names. May be specified multiple times.",
        Recursive = true,
        AllowMultipleArgumentsPerToken = true,
    };

    public static readonly Option<DestructiveActionPolicy?> Destructive = new("--destructive")
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

    /// <summary>
    /// The recursive (global) options shared by every command.
    /// </summary>
    public static IReadOnlyList<Option> Global => [Config, ConnectionString, Provider, Schema, SchemaName, Destructive, StateFile];
}
