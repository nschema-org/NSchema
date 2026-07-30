namespace NSchema.Commands.New;

/// <summary>
/// The diagnostics the CLI mints while scaffolding a new project.
/// </summary>
internal static class ScaffoldDiagnostics
{
    internal static readonly DiagnosticSource Source = "scaffold";

    /// <summary>
    /// A target directory that already holds files the scaffold would write over.
    /// </summary>
    public static Diagnostic DirectoryNotEmpty(string directory) =>
        Diagnostic.Error(Source, "directory-not-empty", $"{directory} is not empty. Use --force to override.");

    /// <summary>
    /// A plugin's scaffold prompts that can be neither answered nor defaulted.
    /// </summary>
    public static Diagnostic UnanswerablePrompts(string keys) =>
        Diagnostic.Error(Source, "unanswerable-prompts",
            $"No terminal to prompt on, and no value given for: {keys}. Supply each with --set <key>=<value>, or run interactively.");
}
