using NSchema.Configuration.Provider;
using NSchema.Configuration.State;
using NSchema.Diff.Policies;

namespace NSchema.Configuration.Ddl;

/// <summary>
/// Configuration read from the blocks in the project's <c>.sql</c> files.
/// </summary>
internal sealed class DdlProjectConfig
{
    /// <summary>
    /// The live-database provider. Null when none is declared.
    /// </summary>
    public ProviderConfig? Provider { get; init; }

    /// <summary>
    /// The state backend. Null when none is declared.
    /// </summary>
    public StateConfig? State { get; init; }

    /// <summary>
    /// The destructive-action policy. Null when unset.
    /// </summary>
    public DestructiveActionPolicy? DestructiveActionPolicy { get; init; }
}
