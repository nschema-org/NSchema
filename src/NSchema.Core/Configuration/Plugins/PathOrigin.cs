namespace NSchema.Configuration.Plugins;

/// <summary>
/// A plugin loaded straight from a built assembly on disk, with no package, feed or lockfile involved.
/// </summary>
/// <param name="Path">The path to the plugin assembly, as written in the declaration.</param>
public sealed record PathOrigin(string Path) : PluginOrigin;
