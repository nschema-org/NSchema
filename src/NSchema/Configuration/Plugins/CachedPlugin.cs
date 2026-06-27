namespace NSchema.Configuration.Plugins;

/// <summary>
/// A restored plugin package version found in the cache.
/// </summary>
internal sealed record CachedPlugin(string PackageId, string Version, string Path, long SizeBytes);
