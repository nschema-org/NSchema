using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A resolved reference to the NuGet package that supplies a plugin.
/// </summary>
/// <param name="PackageId">The NuGet package id, from the <c>PLUGIN</c> declaration's <c>source</c>.</param>
/// <param name="Version">The concrete version the declaration resolved to.</param>
/// <param name="Label">The local label the configuration declares the plugin under.</param>
/// <param name="Settings">The configuring statement's translated settings, handed to the plugin verbatim.</param>
internal sealed record PluginReference(PackageId PackageId, SemanticVersion Version, PluginLabel Label, PluginSettings Settings)
{
    /// <summary>
    /// Resolves a <c>DATABASE</c>/<c>STATE</c> statement's label against the declared <c>PLUGIN</c> dependencies,
    /// pinning a declared range to a concrete version through <paramref name="resolve"/>.
    /// </summary>
    /// <param name="config">The statement's translated settings, carrying the label.</param>
    /// <param name="plugins">The project's <c>PLUGIN</c> declarations.</param>
    /// <param name="resolve">Resolves a declared version (exact or range) to the concrete version to use.</param>
    /// <returns>The resolved reference, or a failure when the label names no declared plugin or the version will not resolve.</returns>
    public static Result<PluginReference> Resolve(PluginSettings config, IReadOnlyList<PluginDeclaration> plugins, Func<PackageId, VersionRange, Result<SemanticVersion>> resolve)
    {
        // The config assembly already rejects an unlabelled or unknown reference; the built-in 'file' label is
        // handled by StateConfiguration before this is reached.
        var label = config.Label!;
        var declaration = plugins.FirstOrDefault(p => p.Label == label);
        if (declaration is null)
        {
            return Diagnostic.Error(label.Value,
                $"'{label}' does not reference a declared plugin. Add: PLUGIN {label} ( source = '...', version = '...' );");
        }

        var version = resolve(declaration.Package.Source, declaration.Package.Version);

        return version.IsFailure
            ? Result.Failure<PluginReference>(version.Diagnostics)
            : new PluginReference(declaration.Package.Source, version.Require(), label, config);
    }
}
