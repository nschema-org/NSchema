using NSchema.Configuration.Plugins;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig
{
    private const string FileLabel = "file";

    /// <summary>
    /// Local-file state store settings (built in; needs no plugin).
    /// </summary>
    public FileStateConfig? File { get; set; }

    /// <summary>
    /// The resolved state-store plugin reference (e.g. <c>s3</c>); <see langword="null"/> for the built-in file store.
    /// </summary>
    public PluginReference? Plugin { get; set; }

    /// <summary>
    /// Maps a <c>BACKEND</c> block onto either the built-in file store or a resolved plugin reference.
    /// </summary>
    public static StateConfig FromBlock(ConfigBlock block) =>
        string.Equals(block.Label, FileLabel, StringComparison.OrdinalIgnoreCase)
            ? new StateConfig { File = FileStateConfig.FromBlock(block) }
            : new StateConfig { Plugin = PluginReference.ForBackend(block) };
}
