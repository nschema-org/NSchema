namespace NSchema.Configuration.Plugins;

/// <summary>
/// A plugin a project pins, annotated with whether it is restored in the cache. Backs <c>plugin list</c> / <c>plugin
/// show</c>.
/// </summary>
internal sealed record ProjectPlugin(
    string Role,
    string Label,
    string PackageId,
    string Version,
    bool Restored,
    string? CachePath
);
