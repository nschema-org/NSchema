using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A package, pinned to the version the lockfile or the declaration settled on.
/// </summary>
internal sealed record ResolvedPackage(SemanticVersion Version) : ResolvedOrigin;
