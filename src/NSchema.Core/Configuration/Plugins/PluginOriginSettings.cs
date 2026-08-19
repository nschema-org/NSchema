using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// The bindable shape of a <c>PLUGIN</c> statement's attributes, before it is known which kind of origin was
/// declared.
/// </summary>
internal sealed record PluginOriginSettings
{
    /// <summary>
    /// The package id, for a package origin.
    /// </summary>
    public PackageId? Source { get; init; }

    /// <summary>
    /// The version range to resolve within, for a package origin.
    /// </summary>
    public VersionRange? Version { get; init; }

    /// <summary>
    /// The plugin assembly's path, for a path origin.
    /// </summary>
    public string? Path { get; init; }
}
