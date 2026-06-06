using System.CommandLine;
using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Configuration.State;

internal static class StateOptions
{
    public static readonly OptionBinding<string> File = OptionBinding.Create<string>()
        .FromOption("--state-file")
        .FromEnvironmentVariable(EnvironmentVariables.StateFile)
        .WithDescription("Path for a file state store.");

    public static readonly OptionBinding<string> S3Bucket = OptionBinding.Create<string>()
        .FromOption("--state-s3-bucket")
        .FromEnvironmentVariable(EnvironmentVariables.StateS3Bucket)
        .WithDescription("Bucket for an S3 state store.");

    public static readonly OptionBinding<string> S3Key = OptionBinding.Create<string>()
        .FromOption("--state-s3-key")
        .FromEnvironmentVariable(EnvironmentVariables.StateS3Key)
        .WithDescription("Object key for an S3 state store.");

    public static IEnumerable<Option> All => [File.Option, S3Bucket.Option, S3Key.Option];
}
