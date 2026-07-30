using NSchema.Project.Nsql;

namespace NSchema.Configuration;

/// <summary>
/// The generic diagnostics minted when assembling a configuration; capability-specific findings live with their
/// capability (see <c>Engine.EngineDiagnostics</c> and <c>Plugins.PluginDiagnostics</c>).
/// </summary>
internal static class ConfigurationDiagnostics
{
    internal static readonly DiagnosticSource Source = "config";

    /// <summary>
    /// A second statement of a kind the configuration holds at most one of.
    /// </summary>
    public static NsqlDiagnostic DuplicateStatement(string statement, SourcePosition position) =>
        new(Source, "duplicate-statement", $"A configuration holds at most one {statement:text} statement.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A <c>DATABASE</c>/<c>STATE</c> statement with no label, so it names no plugin.
    /// </summary>
    public static NsqlDiagnostic UnlabelledReference(string statement, SourcePosition position) =>
        new(Source, "unlabelled-reference", $"A {statement:text} statement needs a label naming the plugin that serves it.", DiagnosticSeverity.Error, position);
}
