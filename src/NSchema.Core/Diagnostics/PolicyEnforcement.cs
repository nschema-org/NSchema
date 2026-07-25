namespace NSchema.Diagnostics;

/// <summary>
/// How the migration pipeline reacts when a policy reports a finding.
/// </summary>
public enum PolicyEnforcement
{
    /// <summary>
    /// Findings are reported as errors, blocking the migration.
    /// </summary>
    Error,

    /// <summary>
    /// Findings are reported as warnings; the migration proceeds.
    /// </summary>
    Warn,

    /// <summary>
    /// Findings are reported as informational only.
    /// </summary>
    Allow,

    /// <summary>
    /// Findings are not reported at all.
    /// </summary>
    Ignore
}
