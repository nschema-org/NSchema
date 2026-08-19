namespace NSchema.Configuration.Plugins;

/// <summary>
/// A declared plugin dependency.
/// </summary>
/// <param name="Label">The local name the plugin is referenced by.</param>
/// <param name="Origin">Where the plugin comes from: a package to resolve, or an assembly path to load.</param>
public sealed record PluginDeclaration(PluginLabel Label, PluginOrigin Origin)
{
    /// <summary>
    /// The package this plugin resolves from, or <see langword="null"/> when it is loaded from a path.
    /// </summary>
    /// <remarks>
    /// A convenience prop for things that only care about packages.
    /// </remarks>
    public PackageReference? Package => (Origin as PackageOrigin)?.Package;
}
