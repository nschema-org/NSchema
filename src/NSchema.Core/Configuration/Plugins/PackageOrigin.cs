namespace NSchema.Configuration.Plugins;

/// <summary>
/// A plugin supplied by a NuGet package, resolved through the configured feeds and pinned by the lockfile.
/// </summary>
/// <param name="Package">The package coordinate to resolve within.</param>
public sealed record PackageOrigin(PackageReference Package) : PluginOrigin;
