using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Configuration;

/// <summary>
/// Configuration read from the project's configuration files.
/// </summary>
internal sealed class ProjectConfig
{
    /// <summary>
    /// The database plugin reference (the <c>DATABASE</c> statement). Null when none is declared.
    /// </summary>
    public PluginReference? Provider { get; init; }

    /// <summary>
    /// The state backend (the <c>STATE</c> statement). Null when none is declared.
    /// </summary>
    public StateConfig? State { get; init; }
}
