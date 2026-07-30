namespace NSchema.Project.Nsql;

/// <summary>
/// The diagnostics minted when reading NSchema source.
/// </summary>
internal static class NsqlDiagnostics
{
    internal static readonly DiagnosticSource Source = "syntax";
    internal static readonly DiagnosticSource FormatSource = "format";

    /// <summary>
    /// A source document that could not be lexed or parsed.
    /// </summary>
    public static NsqlDiagnostic Syntax(NsqlSyntaxException exception) =>
        new(Source, "syntax", exception.Message, DiagnosticSeverity.Error, exception.Position);

    /// <summary>
    /// A statement whose layout is not canonical — what a rewrite would change. A warning, not an error: the
    /// value is still valid, just not formatted.
    /// </summary>
    public static NsqlDiagnostic Formatting(SourcePosition position) =>
        new(FormatSource, "formatting", "This statement is not canonically formatted.", DiagnosticSeverity.Warning, position)
        { Kind = DiagnosticKind.Advisory };

    /// <summary>
    /// A file that could not be read at all. A file-level finding has no position in the source; it points
    /// at the top of the file.
    /// </summary>
    public static NsqlDiagnostic UnreadableFile(string path, Exception exception) => new(
        Source,
        "unreadable-source-file",
        $"Could not read '{path}': {exception.Message:text}",
        DiagnosticSeverity.Error,
        new SourcePosition(0, 1, 1)
    )
    {
        File = path,
    };
}
