using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;

namespace NSchema.Commands.Destroy;

internal static class DestroyOptions
{
    public static readonly OptionBinding<ProviderType> Provider = OptionBinding.Create<ProviderType>()
        .FromOption("--provider")
        .FromEnvironmentVariable(EnvironmentVariables.Provider)
        .WithDescription("Database provider the teardown is generated and executed against (e.g. postgres).");

    public static readonly OptionBinding<string> ConnectionString = OptionBinding.Create<string>()
        .FromOption("--connection-string")
        .FromEnvironmentVariable(EnvironmentVariables.ConnectionString)
        .WithDescription("Connection string for the database the managed schema is dropped from.");

    public static readonly OptionBinding<string> SchemaDirectory = OptionBinding.Create<string>()
        .FromOption("--schema-dir")
        .WithDescription("Directory containing the managed-schema files, used as the teardown source when no state store is configured.");

    public static readonly OptionBinding<SchemaFormat> SchemaFormat = OptionBinding.Create<SchemaFormat>()
        .FromOption("--format")
        .WithDescription("The format the managed schema is expressed in: yaml (default) or json.");

    public static readonly OptionBinding<string> SchemaPattern = OptionBinding.Create<string>()
        .FromOption("--schema-pattern")
        .WithDescription("Glob matched within the schema directory. Defaults to a per-format pattern (e.g. **/*.yaml).");

    public static readonly OptionBinding<string> StateFile = OptionBinding.Create<string>()
        .FromOption("--state-file")
        .FromEnvironmentVariable(EnvironmentVariables.StateFile)
        .WithDescription("Path for a file state store the managed schema is read from and the post-destroy snapshot written to.");

    public static readonly OptionBinding<string> StateS3Bucket = OptionBinding.Create<string>()
        .FromOption("--state-s3-bucket")
        .FromEnvironmentVariable(EnvironmentVariables.StateS3Bucket)
        .WithDescription("Bucket for an S3 state store the managed schema is read from and the post-destroy snapshot written to.");

    public static readonly OptionBinding<string> StateS3Key = OptionBinding.Create<string>()
        .FromOption("--state-s3-key")
        .FromEnvironmentVariable(EnvironmentVariables.StateS3Key)
        .WithDescription("Object key for an S3 state store the managed schema is read from and the post-destroy snapshot written to.");

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the teardown to specific database schemas (namespaces). May be specified multiple times.");

    public static readonly OptionBinding<bool> AutoApprove = OptionBinding.Create<bool>()
        .FromOption("--auto-approve")
        .WithDescription("Skip the interactive confirmation prompt and tear down the schema immediately.");

    public static IEnumerable<Option> All =>
    [
        Provider.Option,
        ConnectionString.Option,
        SchemaDirectory.Option,
        SchemaFormat.Option,
        SchemaPattern.Option,
        StateFile.Option,
        StateS3Bucket.Option,
        StateS3Key.Option,
        Scope.Option,
        AutoApprove.Option,
    ];
}
