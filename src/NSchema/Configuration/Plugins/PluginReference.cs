using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A resolved reference to the plugin a label names.
/// </summary>
/// <param name="PackageId">
/// The plugin assembly's simple name — the package id for a package, and the file name for a path, which is the same
/// thing in both cases: what the load context is asked for once its closure is in place.
/// </param>
/// <param name="Origin">Where to load it from.</param>
/// <param name="Label">The local label the configuration declares the plugin under.</param>
/// <param name="Settings">The configuring statement's translated settings, handed to the plugin verbatim.</param>
internal sealed record PluginReference(PackageId PackageId, ResolvedOrigin Origin, PluginLabel Label, PluginSettings Settings)
{
    /// <summary>
    /// The version this reference pins, or <see langword="null"/> when it is loaded from a path.
    /// </summary>
    public SemanticVersion? Version => (Origin as ResolvedPackage)?.Version;

    /// <summary>
    /// Resolves a <c>DATABASE</c>/<c>STATE</c> statement's label against the declared <c>PLUGIN</c> dependencies,
    /// pinning a declared range to a concrete version through <paramref name="resolve"/>.
    /// </summary>
    /// <param name="config">The statement's translated settings, carrying the label.</param>
    /// <param name="plugins">The project's <c>PLUGIN</c> declarations.</param>
    /// <param name="root">The project root, which a declared relative path is resolved against.</param>
    /// <param name="resolve">Resolves a declared version (exact or range) to the concrete version to use.</param>
    /// <returns>The resolved reference, or a failure when the label names no declared plugin or the version will not resolve.</returns>
    public static Result<PluginReference> Resolve(
        PluginSettings config,
        IReadOnlyList<PluginDeclaration> plugins,
        string root,
        Func<PackageId, VersionRange, Result<SemanticVersion>> resolve
    )
    {
        // The config assembly already rejects an unlabelled or unknown reference; the built-in 'file' label is
        // handled by StateConfiguration before this is reached.
        var label = config.Label!;
        var declaration = plugins.FirstOrDefault(p => p.Label == label);
        if (declaration is null)
        {
            return PluginDiagnostics.NotDeclared(label);
        }

        switch (declaration.Origin)
        {
            case PathOrigin path:
            {
                // Absolute here rather than at load time: the project root is known here, and a diagnostic that
                // quotes the path the loader actually tried is worth more than one quoting what was written.
                var assemblyPath = Path.GetFullPath(path.Path, root);
                var name = Path.GetFileNameWithoutExtension(assemblyPath);

                if (!PackageId.IsValid(name))
                {
                    return PluginDiagnostics.UnusablePluginPath(label, assemblyPath);
                }

                // Checked here rather than at load, because here is where the label and the project root are both
                // known: a diagnostic can say which plugin, and quote the absolute path that was actually tried.
                if (!File.Exists(assemblyPath))
                {
                    return PluginDiagnostics.PluginPathNotFound(label, assemblyPath);
                }

                // A plugin built without EnableDynamicLoading leaves the assembly on disk with none of its closure.
                // The runtime's own account of that is a resolution failure naming a component nobody mentioned, so
                // the far more likely cause is named while there is still something useful to say about it.
                if (!File.Exists(Path.ChangeExtension(assemblyPath, ".deps.json")))
                {
                    return PluginDiagnostics.PluginPathNotSelfContained(label, assemblyPath);
                }

                // The warning rides on the resolved reference rather than being raised at load, so it reaches every
                // command that reads the configuration — a CI log has to show that a run used a build, not a release.
                return Result.Success(
                    new PluginReference(new PackageId(name), new ResolvedPath(assemblyPath), label, config),
                    PluginDiagnostics.PluginLoadedFromPath(label, assemblyPath));
            }

            case PackageOrigin package:
            {
                var version = resolve(package.Package.Source, package.Package.Version);

                return version.IsFailure
                    ? Result.Failure<PluginReference>(version.Diagnostics)
                    : new PluginReference(package.Package.Source, new ResolvedPackage(version.Require()), label, config);
            }

            default:
                throw new NotSupportedException($"Unknown plugin origin '{declaration.Origin.GetType().Name}'.");
        }
    }
}
