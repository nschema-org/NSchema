using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Configuration.Binding;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig : IBindable
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

    public void Bind(ParseResult result)
    {
        StateOptions.File.Bind(result, p => (File ??= new FileStateConfig()).Path = p);
        StateOptions.S3Bucket.Bind(result, b => (S3 ??= new S3StateConfig()).Bucket = b);
        StateOptions.S3Key.Bind(result, k => (S3 ??= new S3StateConfig()).Key = k);
    }
}
