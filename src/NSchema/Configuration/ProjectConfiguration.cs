using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Configuration;

/// <summary>
/// Configuration read from the project's configuration files.
/// </summary>
internal sealed class ProjectConfiguration
{
    /// <summary>
    /// The declared plugin dependencies (the <c>PLUGIN</c> statements), carrying each one's version range.
    /// </summary>
    public IReadOnlyList<PluginDeclaration> Plugins { get; init; } = [];

    /// <summary>
    /// The database plugin reference (the <c>DATABASE</c> statement). Null when none is declared.
    /// </summary>
    public PluginReference? Database { get; init; }

    /// <summary>
    /// The state backend (the <c>STATE</c> statement). Null when none is declared.
    /// </summary>
    public StateConfiguration? State { get; init; }

    /// <summary>
    /// The resolved version of every referenced plugin, to record in the lockfile.
    /// </summary>
    public IReadOnlyList<LockedPlugin> ResolvedPlugins() =>
        ReferencedPlugins()
            .Select(reference => new LockedPlugin { Source = reference.PackageId, Version = reference.Version })
            .ToList();

    private IEnumerable<PluginReference> ReferencedPlugins()
    {
        if (Database != null)
        {
            yield return Database;
        }

        if (State?.Plugin is { } backend)
        {
            yield return backend;
        }
    }
}
