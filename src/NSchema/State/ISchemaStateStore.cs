using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// Persists and retrieves a serialized snapshot of the current database schema, so migration plans can be computed offline.
/// </summary>
/// <remarks>
/// This mirrors Terraform's state backend: an apply writes the resulting schema here, and a state-backed
/// current-state provider reads it back for offline planning.
/// </remarks>
public interface ISchemaStateStore
{
    /// <summary>
    /// Reads the persisted schema snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted schema, or <see langword="null"/> if no state exists yet (bootstrap).</returns>
    Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a schema snapshot, replacing any existing state.
    /// </summary>
    /// <param name="schema">The schema to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default);
}
