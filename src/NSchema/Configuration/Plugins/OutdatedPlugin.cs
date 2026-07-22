using NSchema.Configuration.Model;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A project plugin measured against the feed: the pinned <paramref name="Current"/> version, the
/// <paramref name="Wanted"/> version its declared range would resolve to now, and the <paramref name="Latest"/>
/// available within this host's major. Backs <c>plugin outdated</c>.
/// </summary>
internal sealed record OutdatedPlugin(
    string Role,
    PluginLabel Label,
    PackageId PackageId,
    SemanticVersion Current,
    SemanticVersion Wanted,
    SemanticVersion Latest,
    bool Outdated
);
