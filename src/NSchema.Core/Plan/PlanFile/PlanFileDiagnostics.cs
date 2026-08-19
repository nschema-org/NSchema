namespace NSchema.Plan.PlanFile;

/// <summary>
/// The diagnostics minted when reading saved plan files.
/// </summary>
internal static class PlanFileDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.PlanFile;

    /// <summary>A plan file that could not be read from disk.</summary>
    public static Diagnostic UnreadableFile(string path, Exception exception) => Diagnostic.Error(Source, "unreadable-plan-file",
        $"Could not read '{path}': {exception.Message:text}");

    /// <summary>A plan file whose payload could not be deserialized.</summary>
    public static Diagnostic InvalidPayload(string path, Exception exception) => Diagnostic.Error(Source, "invalid-payload",
        $"{path}: {exception.Message:text}");
}
