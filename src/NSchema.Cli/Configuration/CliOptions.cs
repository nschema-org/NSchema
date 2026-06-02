using System.CommandLine;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

internal static class CliOptions
{
    public static class Global
    {
        public static readonly Option<string?> Config = new("--config")
        {
            Description = "Path to the NSchema config file. Defaults to ./nschema.json if present.",
            Recursive = true,
        };
    }

    public static class Database
    {
        public static readonly Option<ProviderType?> Provider = new("--provider")
        {
            Description = "Database provider supplying the live schema (e.g. postgres).",
            Recursive = true,
        };

        public static readonly Option<string?> ConnectionString = new("--connection-string")
        {
            Description = "Connection string for the database provider.",
            Recursive = true,
        };
    }

    public static class State
    {
        public static readonly Option<StateType?> Type = new("--state-type")
        {
            Description = "State store type: file (default) or s3.",
            Recursive = true,
        };

        public static readonly Option<string?> ConnectionString = new("--state-connection-string")
        {
            Description = "Connection string for the state store (a path for file, an s3://bucket/key URI for s3).",
            Recursive = true,
        };

        public static readonly Option<string?> File = new("--state-file")
        {
            Description = "Shorthand for a file state store's path (equivalent to --state-type file --state-connection-string <path>).",
            Recursive = true,
        };
    }

    public static class Desired
    {
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
    }

    public static class Migration
    {
        public static readonly Option<string[]> Scope = new("--scope")
        {
            Description = "Limit the migration to specific database schemas (namespaces). May be specified multiple times.",
            AllowMultipleArgumentsPerToken = true,
        };

        public static readonly Option<DestructiveActionPolicy?> Destructive = new("--destructive-actions")
        {
            Description = "Policy for destructive actions: Error (default), Warn, or Allow.",
        };
    }

    public static class Apply
    {
        public static readonly Option<bool> AutoApprove = new("--auto-approve")
        {
            Description = "Skip the interactive confirmation prompt and apply the plan immediately.",
        };
    }

    public static class Init
    {
        public static readonly Option<SchemaFormat> Format = new("--format")
        {
            Description = "Format for the generated config and sample schema: yaml (default) or json.",
        };

        public static readonly Option<bool> Force = new("--force")
        {
            Description = "Overwrite an existing nschema.json.",
        };
    }
}
