namespace NSchema.Project.Nsql;

/// <summary>
/// A finding from reading NSchema source.
/// </summary>
/// <param name="Source">What produced this finding.</param>
/// <param name="Code">What identifies this finding, whatever its wording.</param>
/// <param name="Text">A descriptive message about the finding, with its merged values marked.</param>
/// <param name="Severity">The severity of the finding.</param>
/// <param name="Position">The position in the source where the finding was detected.</param>
public sealed record NsqlDiagnostic(DiagnosticSource Source, DiagnosticCode Code, FormattedText Text, DiagnosticSeverity Severity, SourcePosition Position)
    : Diagnostic(Source, Code, Text, Severity)
{
    /// <summary>
    /// The file the source was read from, or <see langword="null"/> when parsed from raw source.
    /// </summary>
    public string? File { get; init; }
}
