using System.Text.Json.Serialization;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig
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

    /// <summary>
    /// Copies the populated sections from <paramref name="other"/> onto this instance.
    /// </summary>
    public void CopyFrom(StateConfig other)
    {
        File = other.File;
        S3 = other.S3;
    }
}
