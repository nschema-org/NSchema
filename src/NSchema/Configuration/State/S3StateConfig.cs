using NSchema.Configuration.Ddl;

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

    /// <summary>
    /// Maps a <c>BACKEND s3</c> block's attributes onto a new config, rejecting any it doesn't recognise.
    /// </summary>
    public static S3StateConfig FromBlock(ConfigBlock block)
    {
        var config = new S3StateConfig();
        foreach (var (key, value) in block.Attributes)
        {
            switch (key.ToLowerInvariant())
            {
                case "bucket":
                    config.Bucket = value.AsString();
                    break;
                case "key":
                    config.Key = value.AsString();
                    break;
                default:
                    throw block.UnknownAttribute(key);
            }
        }

        return config;
    }
}
