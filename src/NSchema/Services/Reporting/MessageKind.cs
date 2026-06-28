namespace NSchema.Services.Reporting;

/// <summary>
/// Classifies a status / progress message.
/// </summary>
public enum MessageKind
{
    /// <summary>
    /// Low-level detail useful when diagnosing a run but noise in normal output
    /// </summary>
    Verbose,

    /// <summary>
    /// A neutral statement about the operation.
    /// </summary>
    Announcement,

    /// <summary>
    /// A transient progress step ("Loading desired schema...", "Generating SQL...").
    /// </summary>
    Progress,

    /// <summary>
    /// A successful outcome ("Migration completed successfully.", "Schema is valid.").
    /// </summary>
    Success,

    /// <summary>
    /// A non-fatal warning that warrants attention but does not stop the operation
    /// ("Unable to generate SQL preview...", "Drift detected.").
    /// </summary>
    Warning,
}
