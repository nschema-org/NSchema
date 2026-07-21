using NSchema.Configuration.Model;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A restored plugin package version found in the cache.
/// </summary>
internal sealed record CachedPlugin(PackageId PackageId, SemanticVersion Version, string Path, long SizeBytes);
