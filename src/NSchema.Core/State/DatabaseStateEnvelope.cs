using NSchema.Model;
using NSchema.State.Domain;

namespace NSchema.State;

/// <summary>
/// The versioned wrapper persisted by the state store.
/// </summary>
/// <param name="Version">The version of the envelope format.</param>
/// <param name="Database">The captured database structure..</param>
internal sealed record DatabaseStateEnvelope(int Version, Database Database)
{
    /// <summary>
    /// The current envelope format version.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The recorded script executions.
    /// </summary>
    public IReadOnlyList<ScriptExecution> Scripts { get; init; } = [];

    /// <summary>
    /// The identities of objects in the full <see cref="Database"/> that we are responsible for managing.
    /// </summary>
    public IdentitySet Managed { get; init; } = IdentitySet.Empty;

    /// <summary>
    /// The declared spellings recorded for the managed body-bearing objects.
    /// </summary>
    public DefinitionSet Declared { get; init; } = DefinitionSet.Empty;
}
