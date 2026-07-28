namespace NSchema.State.Locks;

/// <summary>
/// Represents a held state lock.
/// </summary>
public interface IStateLockHandle
{
    /// <summary>
    /// Metadata about the held lock.
    /// </summary>
    StateLockInfo Info { get; }

    /// <summary>
    /// Releases the held lock. Idempotent: releasing more than once is a no-op.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>Success, or a failure describing why the lock could not be released.</returns>
    ValueTask<Result> Release(CancellationToken cancellationToken = default);
}
