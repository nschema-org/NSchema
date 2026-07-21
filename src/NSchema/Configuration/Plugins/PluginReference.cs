using NSchema.Plugins.Model;
using NSchema.Plugins.Model.Config;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A resolved reference to the NuGet package that supplies a plugin.
/// </summary>
/// <param name="PackageId">The NuGet package id, from the <c>PLUGIN</c> declaration's <c>source</c>.</param>
/// <param name="Version">The pinned version as the user reads it (used for display and the cache layout).</param>
/// <param name="RestoreVersion">The version in NuGet range notation, fed to package restore.</param>
/// <param name="Label">The local label the configuration declares the plugin under.</param>
/// <param name="Config">The configuring statement's translated settings, handed to the plugin verbatim.</param>
internal sealed record PluginReference(string PackageId, string Version, string RestoreVersion, string Label, PluginConfig Config)
{
    /// <summary>
    /// Resolves a <c>DATABASE</c>/<c>STATE</c> statement's label against the declared <c>PLUGIN</c> dependencies.
    /// </summary>
    /// <param name="config">The statement's translated settings, carrying the label.</param>
    /// <param name="plugins">The project's <c>PLUGIN</c> declarations.</param>
    public static PluginReference Resolve(PluginConfig config, IReadOnlyList<PluginDeclaration> plugins)
    {
        // The config assembly already rejects an unlabelled or unknown reference; the built-in 'file' label is
        // handled by StateConfig before this is reached.
        var label = config.Label!;
        var declaration = plugins.FirstOrDefault(p => p.Label == label)
            ?? throw new InvalidOperationException(
                $"'{label}' does not reference a declared plugin. Add: PLUGIN {label} ( source = '...', version = '...' );");

        var (version, restoreVersion) = Versions(declaration.Version);
        return new PluginReference(declaration.Source.Value, version, restoreVersion, label.Value, config);
    }

    // An exact pin canonicalizes to "[x.y.z]"; display (and the cache layout) keeps the bare version the user
    // wrote, while restore gets the NuGet notation so an exact pin stays exact. A real range uses its canonical
    // interval text for both.
    private static (string Version, string RestoreVersion) Versions(VersionRange range)
    {
        var text = range.ToString();
        return text.StartsWith('[') && text.EndsWith(']') && !text.Contains(',')
            ? (text[1..^1], text)
            : (text, text);
    }
}
