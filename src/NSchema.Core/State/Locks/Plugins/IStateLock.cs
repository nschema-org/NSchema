namespace NSchema.State.Locks.Plugins;

/// <summary>
/// Coordinates exclusive access to the shared state.
/// </summary>
public interface IStateLock
{
    /// <summary>
    /// Acquires the lock with the supplied metadata, blocking other operations until the returned handle is released.
    /// </summary>
    /// <param name="lockInfo">The metadata to record for the acquired lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A handle that releases the lock when disposed..</returns>
    /// <exception cref="StateLockedException">
    /// The lock is already held by another operation. Contention keeps a typed signal rather than a plain failure
    /// because it carries the holder's details, which the engine turns into the message the operator reads.
    /// </exception>
    Task<Result<IStateLockHandle>> Acquire(StateLockInfo lockInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the currently-held lock without acquiring or modifying it.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The held lock (<see langword="null"/> when the state is not locked), or a failure.</returns>
    Task<Result<LockPeekResult>> Peek(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <summary>
    /// Releases whatever lock is currently held.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    ValueTask<Result> Release(CancellationToken cancellationToken = default);
}
