using NSchema.Configuration.Domain;
using NSchema.Project.Nsql;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// The diagnostics minted for the plugin configuration: declaring plugins, resolving the lockfile, and binding a
/// <see cref="PluginSettings"/> onto an options object.
/// </summary>
internal static class PluginDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Plugins;

    /// <summary>
    /// A plugin label declared by more than one <c>PLUGIN</c> statement.
    /// </summary>
    public static NsqlDiagnostic DuplicatePluginLabel(PluginLabel label, SourcePosition position) =>
        new(Source, "duplicate-plugin-label", $"Plugin '{label}' is declared more than once.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A package declared by more than one <c>PLUGIN</c> statement.
    /// </summary>
    public static NsqlDiagnostic DuplicatePluginSource(PackageId source, SourcePosition position) =>
        new(Source, "duplicate-plugin-source", $"Package '{source}' is declared by more than one PLUGIN statement; a package is declared once and referenced by its label.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A <c>DATABASE</c>/<c>STATE</c> label that no <c>PLUGIN</c> statement (or host built-in) declares.
    /// </summary>
    public static NsqlDiagnostic UnknownPluginLabel(string statement, PluginLabel label, SourcePosition position) =>
        new(Source, "unknown-plugin-label", $"{statement:text} references plugin '{label}', but no PLUGIN statement declares it.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A plugin declared with a version range that the lockfile does not pin to a concrete version.
    /// </summary>
    public static Diagnostic PluginNotLocked(PackageId source, VersionRange range) => Diagnostic.Error(Source, "plugin-not-locked",
        $"Plugin '{source}' is declared with version range '{range}' but is not locked.");

    /// <summary>
    /// A <c>PLUGIN</c> statement declaring both a package and a path.
    /// </summary>
    public static NsqlDiagnostic ConflictingPluginOrigin(PluginLabel label, SourcePosition position) =>
        new(Source, "conflicting-plugin-origin",
            $"Plugin '{label}' mixes 'path' with the package attributes 'source' and 'version'. A plugin comes from a package or from a built assembly, not both.",
            DiagnosticSeverity.Error, position);

    /// <summary>
    /// A <c>PLUGIN</c> statement declaring neither a package nor a path.
    /// </summary>
    public static NsqlDiagnostic MissingPluginOrigin(PluginLabel label, SourcePosition position) =>
        new(Source, "missing-plugin-origin",
            $"Plugin '{label}' declares no origin; give it 'source' and 'version' to resolve a package, or 'path' to load a built assembly.",
            DiagnosticSeverity.Error, position);
}
