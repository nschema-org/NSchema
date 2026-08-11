namespace NSchema.Configuration.Plugins;

/// <summary>
/// The diagnostics minted when reading and writing the plugin lockfile.
/// </summary>
internal static class LockFileDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.LockFile;

    /// <summary>
    /// The lockfile could not be written.
    /// </summary>
    public static Diagnostic Unwritable(string path, string reason) =>
        Diagnostic.Error(Source, "unwritable", $"Could not write the lockfile '{path}': {reason:text}");
}
