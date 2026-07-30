namespace NSchema.Commands.Import;

/// <summary>
/// The diagnostics the CLI mints while importing an existing database into project files.
/// </summary>
internal static class ImportDiagnostics
{
    internal static readonly DiagnosticSource Source = "import";

    /// <summary>
    /// An output directory that already holds schema files the import would write over.
    /// </summary>
    public static Diagnostic OutputNotEmpty(string directory) =>
        Diagnostic.Error(Source, "output-not-empty",
            $"{directory} already contains .sql files that import would overwrite. Re-run with --force to overwrite.");
}
