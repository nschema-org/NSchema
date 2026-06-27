using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Configuration.Ddl;

/// <summary>
/// Configuration read from the blocks in the project's <c>.sql</c> files.
/// </summary>
internal sealed class DdlProjectConfig
{
    /// <summary>
    /// The live-database provider plugin reference. Null when none is declared.
    /// </summary>
    public PluginReference? Provider { get; init; }

    /// <summary>
    /// The state backend. Null when none is declared.
    /// </summary>
    public StateConfig? State { get; init; }
}
