namespace NSchema.Cli.Configuration;

/// <summary>
/// Configures an Amazon S3 state store.
/// </summary>
internal sealed class S3StateConfig
{
    /// <summary>
    /// The S3 bucket that holds the state object.
    /// </summary>
    public string? Bucket { get; set; }

    /// <summary>
    /// The S3 object key for the state file within the bucket.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Validates the configuration, yielding one message per problem found.
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Bucket))
        {
            yield return "state.s3.bucket is required.";
        }

        if (string.IsNullOrWhiteSpace(Key))
        {
            yield return "state.s3.key is required.";
        }
    }
}
