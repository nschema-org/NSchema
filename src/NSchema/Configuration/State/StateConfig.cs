using NSchema.Configuration.Plugins;
using NSchema.Plugins.Model;
using NSchema.Plugins.Model.Config;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig
{
    private static readonly PluginLabel _fileLabel = new("file");

    /// <summary>
    /// Local-file state store settings (built in; needs no plugin).
    /// </summary>
    public FileStateConfig? File { get; set; }

    /// <summary>
    /// The resolved state-store plugin reference; <see langword="null"/> for the built-in file store.
    /// </summary>
    public PluginReference? Plugin { get; set; }

    /// <summary>
    /// Maps a <c>STATE</c> statement onto either the built-in file store or a resolved plugin reference.
    /// </summary>
    /// <param name="config">The statement's translated settings.</param>
    /// <param name="plugins">The project's <c>PLUGIN</c> declarations.</param>
    public static StateConfig Resolve(PluginConfig config, IReadOnlyList<PluginDeclaration> plugins) =>
        config.Label == _fileLabel
            ? new StateConfig { File = FileStateConfig.FromConfig(config) }
            : new StateConfig { Plugin = PluginReference.Resolve(config, plugins) };
}
