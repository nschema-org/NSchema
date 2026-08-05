using NSchema.Model;
using NSchema.State.Domain;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Defines what state currently exists in the context of a migration.
/// </summary>
/// <param name="Database">The current database structure.</param>
/// <param name="ExecutedScripts">The recorded script executions.</param>
/// <param name="Managed">The identities NSchema manages.</param>
/// <param name="Declared">The declared spellings recorded for the managed body-bearing objects.</param>
internal sealed record CurrentState(
    Database Database,
    IReadOnlyList<ScriptExecution> ExecutedScripts,
    IdentitySet? Managed = null,
    DefinitionSet? Declared = null
)
{
    /// <summary>
    /// Creates a state carrying only the database structure, with no recorded executions and nothing managed.
    /// </summary>
    public CurrentState(Database database) : this(database, []) { }

    /// <summary>
    /// The identities NSchema manages.
    /// </summary>
    public IdentitySet Managed { get; init; } = Managed ?? IdentitySet.Empty;

    /// <summary>
    /// The declared spellings recorded for the managed body-bearing objects.
    /// </summary>
    public DefinitionSet Declared { get; init; } = Declared ?? DefinitionSet.Empty;

    /// <summary>
    /// Narrows the current state to the given identity set.
    /// </summary>
    /// <param name="identities">The identity set to narrow the state to.</param>
    /// <returns>The state, narrowed to the given identity set.</returns>
    public CurrentState FilteredTo(IdentitySet identities)
    {
        return this with { Database = Database.FilteredTo(identities) };
    }
}
