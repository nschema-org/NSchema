using System.CommandLine;

namespace NSchema.Cli.Configuration.State;

internal static class StateOptions
{
    public static readonly Option<string> File = new("--state-file")
    {
        Description = "Path for a file state store.",
    };

    public static readonly Option<string> S3Bucket = new("--state-s3-bucket")
    {
        Description = "Bucket for an S3 state store.",
    };

    public static readonly Option<string> S3Key = new("--state-s3-key")
    {
        Description = "Object key for an S3 state store.",
    };
}
