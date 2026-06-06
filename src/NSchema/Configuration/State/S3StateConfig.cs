namespace NSchema.Configuration.State;

/// <summary>
/// Configures an Amazon S3 state store.
/// </summary>
internal sealed class S3StateConfig
{
    /// <summary>
    /// The S3 bucket that holds the state object.
    /// </summary>
    public string Bucket { get; set; } = "";

    /// <summary>
    /// The S3 object key for the state file within the bucket.
    /// </summary>
    public string Key { get; set; } = "";
}
