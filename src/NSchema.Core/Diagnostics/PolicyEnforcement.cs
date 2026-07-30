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

/// <summary>
/// Rendering for <see cref="PolicyEnforcement"/>.
/// </summary>
internal static class PolicyEnforcementExtensions
{
    /// <summary>
    /// The severity the enforcement reports at, or <see langword="null"/> when it reports nothing at all.
    /// </summary>
    public static DiagnosticSeverity? Severity(this PolicyEnforcement enforcement) => enforcement switch
    {
        PolicyEnforcement.Error => DiagnosticSeverity.Error,
        PolicyEnforcement.Warn => DiagnosticSeverity.Warning,
        PolicyEnforcement.Allow => DiagnosticSeverity.Info,
        PolicyEnforcement.Ignore => null,
        _ => throw new ArgumentOutOfRangeException(nameof(enforcement), enforcement, null),
    };
}
