namespace NSchema.Cli.Configuration.State;

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
    /// The store selected by the populated section, or null when none is configured.
    /// </summary>
    public StateType? SelectedType => File is not null ? StateType.File
        : S3 is not null ? StateType.S3
        : null;

    /// <summary>
    /// The number of populated store sections. Used to enforce the "exactly one" rule.
    /// </summary>
    public int ConfiguredSectionCount => (File is null ? 0 : 1) + (S3 is null ? 0 : 1);
}
