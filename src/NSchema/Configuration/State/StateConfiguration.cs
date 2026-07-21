using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfiguration
{
    private static readonly PluginLabel _fileLabel = new("file");

    /// <summary>
    /// Local-file state store settings (built in; needs no plugin).
    /// </summary>
    public FileStateConfiguration? File { get; set; }

    /// <summary>
    /// The resolved state-store plugin reference; <see langword="null"/> for the built-in file store.
    /// </summary>
    public PluginReference? Plugin { get; set; }

    /// <summary>
    /// Maps a <c>STATE</c> statement onto either the built-in file store or a resolved plugin reference.
    /// </summary>
    /// <param name="config">The statement's translated settings.</param>
    /// <param name="plugins">The project's <c>PLUGIN</c> declarations.</param>
    /// <param name="resolve">Resolves a declared range to a concrete version.</param>
    public static StateConfiguration Resolve(PluginSettings config, IReadOnlyList<PluginDeclaration> plugins, Func<PackageId, VersionRange, SemanticVersion> resolve) =>
        config.Label == _fileLabel
            ? new StateConfiguration { File = FileStateConfiguration.FromSettings(config) }
            : new StateConfiguration { Plugin = PluginReference.Resolve(config, plugins, resolve) };
}
