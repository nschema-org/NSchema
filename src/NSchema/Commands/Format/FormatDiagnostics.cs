namespace NSchema.Commands.Format;

/// <summary>
/// The diagnostics the CLI mints while formatting project source.
/// </summary>
internal static class FormatDiagnostics
{
    internal static readonly DiagnosticSource Source = "fmt";

    /// <summary>
    /// A path given to <c>fmt</c> that names neither a file nor a directory.
    /// </summary>
    public static Diagnostic PathNotFound(string path) =>
        Diagnostic.Error(Source, "path-not-found", $"No such file or directory: '{path}'.");
}
