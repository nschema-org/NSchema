using System.CommandLine;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.Schema;
using NSchema.Migration;

namespace NSchema.Cli.Configuration;

internal static class CliOptions
{
    public static class Global
    {
        public static readonly Option<string> Config = new("--config")
        {
            Description = "Path to the NSchema config file. Defaults to ./nschema.json if present.",
            Recursive = true,
        };
    }

    public static class Provider
    {
        public static readonly Option<ProviderType> Type = new("--provider")
        {
            Description = "Database provider supplying the live schema (e.g. postgres).",
            Recursive = true,
        };

        public static readonly Option<string> ConnectionString = new("--connection-string")
        {
            Description = "Connection string for the database provider.",
            Recursive = true,
        };
    }

    public static class State
    {
        public static readonly Option<string> File = new("--state-file")
        {
            Description = "Path for a file state store.",
            Recursive = true,
        };

        public static readonly Option<string> S3Bucket = new("--state-s3-bucket")
        {
            Description = "Bucket for an S3 state store.",
            Recursive = true,
        };

        public static readonly Option<string> S3Key = new("--state-s3-key")
        {
            Description = "Object key for an S3 state store.",
            Recursive = true,
        };
    }

    public static class Schema
    {
        public static readonly Option<SchemaFormat> Format = new("--format")
        {
            Description = "The format the desired schema is expressed in: yaml (default) or json.",
        };

        public static readonly Option<string> Directory = new("--schema-dir")
        {
            Description = "Directory containing the desired-schema files. Required for plan and apply unless set in config.",
        };

        public static readonly Option<string> Pattern = new("--schema-pattern")
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

        public static readonly Option<DestructiveActionPolicy> Destructive = new("--destructive-actions")
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
