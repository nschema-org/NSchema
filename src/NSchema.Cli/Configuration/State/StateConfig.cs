using System.CommandLine;
using System.Text.Json.Serialization;

namespace NSchema.Cli.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig : IConfigurable
{
    /// <summary>
    /// Local-file state store settings.
    /// </summary>
    public FileStateConfig? File { get; set; }

    /// <summary>
    /// Amazon S3 state store settings.
    /// </summary>
    public S3StateConfig? S3 { get; set; }

    /// <summary>
    /// The number of state store sections populated. Zero means online-only (no state store).
    /// </summary>
    [JsonIgnore]
    public int ConfiguredSectionCount => (File is not null ? 1 : 0) + (S3 is not null ? 1 : 0);

    public void Configure(ParseResult result)
    {
        if (StateOptions.File.TryResolve(result, out var path))
        {
            File ??= new FileStateConfig();
            File.Path = path;
        }

        if (StateOptions.S3Bucket.TryResolve(result, out var bucket))
        {
            S3 ??= new S3StateConfig();
            S3.Bucket = bucket;
        }

        if (StateOptions.S3Key.TryResolve(result, out var key))
        {
            S3 ??= new S3StateConfig();
            S3.Key = key;
        }
    }
}
