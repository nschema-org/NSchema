using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;

namespace NSchema.Commands.Refresh;

internal static class RefreshOptions
{
    public static readonly OptionBinding<ProviderType> Provider = OptionBinding.Create<ProviderType>()
        .FromOption("--provider")
        .FromEnvironmentVariable(EnvironmentVariables.Provider)
        .WithDescription("Database provider whose live schema is snapshotted into the state store (e.g. postgres).");

    public static readonly OptionBinding<string> ConnectionString = OptionBinding.Create<string>()
        .FromOption("--connection-string")
        .FromEnvironmentVariable(EnvironmentVariables.ConnectionString)
        .WithDescription("Connection string for the database whose live schema is snapshotted.");

    public static readonly OptionBinding<string> StateFile = OptionBinding.Create<string>()
        .FromOption("--state-file")
        .FromEnvironmentVariable(EnvironmentVariables.StateFile)
        .WithDescription("Path for a file state store the live snapshot is written to.");

    public static readonly OptionBinding<string> StateS3Bucket = OptionBinding.Create<string>()
        .FromOption("--state-s3-bucket")
        .FromEnvironmentVariable(EnvironmentVariables.StateS3Bucket)
        .WithDescription("Bucket for an S3 state store the live snapshot is written to.");

    public static readonly OptionBinding<string> StateS3Key = OptionBinding.Create<string>()
        .FromOption("--state-s3-key")
        .FromEnvironmentVariable(EnvironmentVariables.StateS3Key)
        .WithDescription("Object key for an S3 state store the live snapshot is written to.");

    public static IEnumerable<Option> All =>
    [
        Provider.Option,
        ConnectionString.Option,
        StateFile.Option,
        StateS3Bucket.Option,
        StateS3Key.Option,
    ];
}
