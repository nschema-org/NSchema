namespace NSchema.Diagnostics;

/// <summary>
/// The severity of a <see cref="Diagnostic"/> finding.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational; no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// The finding warrants attention but is not, on its own, a failure.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// The finding is a failure; a result that carries one is unsuccessful.
    /// </summary>
    Error = 2
}
