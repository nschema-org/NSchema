using System.CommandLine;
using NSchema.Cli.Configuration.Binding;

namespace NSchema.Cli.Configuration.State;

internal static class StateOptions
{
    public static readonly OptionBinding<string> File = new("--state-file", EnvironmentVariables.StateFile)
    {
        Description = "Path for a file state store.",
    };

    public static readonly OptionBinding<string> S3Bucket = new("--state-s3-bucket", EnvironmentVariables.StateS3Bucket)
    {
        Description = "Bucket for an S3 state store.",
    };

    public static readonly OptionBinding<string> S3Key = new("--state-s3-key", EnvironmentVariables.StateS3Key)
    {
        Description = "Object key for an S3 state store.",
    };

    public static IEnumerable<Option> All => [File.Option, S3Bucket.Option, S3Key.Option];
}
