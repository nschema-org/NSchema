namespace NSchema.Cli.Configuration;

/// <summary>
/// The state store that holds schema state snapshots.
/// </summary>
internal enum StateType
{
    /// <summary>A local file (the default). The connection string is the file path.</summary>
    File,

    /// <summary>
    /// An Amazon S3 object. The connection string is an <c>s3://bucket/key</c> URI.
    /// </summary>
    S3,
}
