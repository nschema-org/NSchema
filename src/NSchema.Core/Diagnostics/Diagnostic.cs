namespace NSchema.Diagnostics;

/// <summary>
/// A single structured finding, <see cref="Result{T}"/>, and the policy diagnostics.
/// </summary>
/// <param name="Source">What produced this finding.</param>
/// <param name="Code">What identifies this finding, whatever its wording.</param>
/// <param name="Text">A descriptive message about the finding, with its merged values marked.</param>
/// <param name="Severity">The severity of the finding.</param>
public record Diagnostic(DiagnosticSource Source, DiagnosticCode Code, FormattedText Text, DiagnosticSeverity Severity)
{
    /// <summary>
    /// What sort of finding this is.
    /// </summary>
    public DiagnosticKind Kind { get; init; } = DiagnosticKind.Structural;

    /// <summary>
    /// The message as plain text.
    /// </summary>
    public string Message => Text.ToString();

    /// <summary>
    /// Creates an informational diagnostic.
    /// </summary>
    /// <param name="source">What produced this finding.</param>
    /// <param name="code">What identifies this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="Diagnostic"/>.</returns>
    public static Diagnostic Info(DiagnosticSource source, DiagnosticCode code, FormattedText message) =>
        new(source, code, message, DiagnosticSeverity.Info) { Kind = DiagnosticKind.Advisory };

    /// <summary>
    /// Creates a warning diagnostic.
    /// </summary>
    /// <param name="source">What produced this finding.</param>
    /// <param name="code">What identifies this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="Diagnostic"/>.</returns>
    public static Diagnostic Warning(DiagnosticSource source, DiagnosticCode code, FormattedText message) =>
        new(source, code, message, DiagnosticSeverity.Warning) { Kind = DiagnosticKind.Advisory };

    /// <summary>
    /// Creates an error diagnostic.
    /// </summary>
    /// <param name="source">What produced this finding.</param>
    /// <param name="code">What identifies this finding.</param>
    /// <param name="message">A descriptive message about the finding.</param>
    /// <returns>The created <see cref="Diagnostic"/>.</returns>
    public static Diagnostic Error(DiagnosticSource source, DiagnosticCode code, FormattedText message) => new(source, code, message, DiagnosticSeverity.Error);

    /// <summary>
    /// Downgrades a diagnostic to a given severity level if it exceeds it.
    /// </summary>
    /// <param name="severity">The downgraded severity level.</param>
    /// <returns>A clone of the current diagnostic capped at the given security level.</returns>
    public Diagnostic Demote(DiagnosticSeverity severity) => Severity > severity ? this with { Severity = severity } : this;
}
