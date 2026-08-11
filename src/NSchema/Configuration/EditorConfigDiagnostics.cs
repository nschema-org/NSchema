namespace NSchema.Configuration;

/// <summary>
/// The diagnostics the CLI mints while reading diagnostic severities out of the project's <c>.editorconfig</c>.
/// </summary>
internal static class EditorConfigDiagnostics
{
    internal static readonly DiagnosticSource Source = "editorconfig";

    /// <summary>
    /// A severity key whose value is not one of the recognised severities.
    /// </summary>
    public static Diagnostic UnknownSeverity(string key, string value) =>
        Diagnostic.Error(Source, "unknown-severity",
            $"{key:text} = {value}: not a severity. Use {EditorConfigKeys.Severities:text}.");

    /// <summary>
    /// A severity key naming something that is not shaped like a diagnostic code or source.
    /// </summary>
    public static Diagnostic InvalidDiagnosticName(string key, string reason) =>
        Diagnostic.Error(Source, "invalid-diagnostic-name", $"{key:text}: {reason:text}");

    /// <summary>
    /// A severity key that resolves to different values for different schema files, which enforcement — being
    /// applied to the run rather than to a file — cannot honour.
    /// </summary>
    public static Diagnostic ScopedSeverity(string key) =>
        Diagnostic.Error(Source, "scoped-severity",
            $"{key:text} must not resolve differently for different schema files.");

    /// <summary>
    /// An <c>.editorconfig</c> that could not be read.
    /// </summary>
    public static Diagnostic Unreadable(string reason) =>
        Diagnostic.Error(Source, "unreadable-editorconfig", $".editorconfig could not be read: {reason:text}");
}
