using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Plugins;

namespace NSchema.Configuration.State;

/// <summary>
/// Configures a backend store used to keep state snapshots.
/// </summary>
internal sealed class StateConfig : IBindable
{
    private const string FileLabel = "file";

    /// <summary>
    /// The first-party backend plugins, keyed by their <c>BACKEND</c> block label.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> BuiltInPackages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["s3"] = "NSchema.Aws",
        };

    /// <summary>
    /// Local-file state store settings (built in; needs no plugin).
    /// </summary>
    public FileStateConfig? File { get; set; }

    /// <summary>
    /// The resolved state-store plugin reference (e.g. <c>s3</c>); <see langword="null"/> for the built-in file
    /// store or when no state store is configured.
    /// </summary>
    public PluginReference? Plugin { get; set; }

    /// <summary>
    /// The number of state stores configured. Zero means online-only (no state store).
    /// </summary>
    public int ConfiguredSectionCount => (File is not null ? 1 : 0) + (Plugin is not null ? 1 : 0);

    /// <summary>
    /// Maps a <c>BACKEND</c> block onto either the built-in file store or a resolved plugin reference.
    /// </summary>
    public static StateConfig FromBlock(ConfigBlock block) =>
        string.Equals(block.Label, FileLabel, StringComparison.OrdinalIgnoreCase)
            ? new StateConfig { File = FileStateConfig.FromBlock(block) }
            : new StateConfig { Plugin = PluginReference.FromBlock(block, BuiltInPackages) };

    /// <summary>
    /// Adopts the state store resolved from the project config (it has no environment or command-line override).
    /// </summary>
    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        File = project.State?.File;
        Plugin = project.State?.Plugin;
    }
}
