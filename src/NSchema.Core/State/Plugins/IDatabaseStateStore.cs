namespace NSchema.State.Plugins;

/// <summary>
/// Persists and retrieves a serialized snapshot of the current database schema, so migration plans can be computed offline.
/// </summary>
public interface IDatabaseStateStore
{
    /// <summary>
    /// Reads the persisted schema snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The persisted snapshot (whose payload is <see langword="null"/> when nothing is recorded yet), or a failure.</returns>
    Task<Result<StoreReadResult>> Read(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a schema snapshot, replacing any existing state.
    /// </summary>
    /// <param name="state">The serialized snapshot to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default);
}
