namespace NSchema.State.Locks;

/// <summary>
/// The consumer-facing surface for managing state locks.
/// </summary>
public interface IStateLockManager
{
    /// <summary>
    /// Takes the state lock described by <paramref name="arguments"/>.
    /// </summary>
    /// <param name="arguments">The lock to take.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<IStateLockHandle>> Acquire(AcquireLockArguments arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the lock currently held against the state, without acquiring it.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<LockPeekResult>> Peek(CancellationToken cancellationToken = default);

    /// <summary>
    /// Force-releases whatever lock is currently held, regardless of who holds it.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result<LockReleaseResult>> Release(CancellationToken cancellationToken = default);
}
