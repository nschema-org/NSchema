using NSchema.State.Locks.Plugins;

namespace NSchema.State.Locks;

/// <summary>
/// The default <see cref="IStateLockManager"/>.
/// </summary>
/// <remarks>
/// Wraps the configured <see cref="IStateLock"/> (when any) with the offline / <c>--no-lock</c> / contention handling,
/// returning a handle the caller releases when done.
/// </remarks>
internal sealed class StateLockManager(IStateLock? stateLock = null) : IStateLockManager
{
    public async Task<Result<LockPeekResult>> Peek(CancellationToken cancellationToken = default)
    {
        if (stateLock is null)
        {
            return new LockPeekResult(null);
        }

        // The backend reports an unreachable lock as a failure; anything it throws is a defect and propagates.
        return await stateLock.Peek(cancellationToken);
    }

    public async Task<Result<IStateLockHandle>> Acquire(AcquireLockArguments arguments, CancellationToken cancellationToken = default)
    {
        // No backend lock to take — this is an ordinary offline run, not a deliberate skip, so say nothing.
        if (stateLock is null)
        {
            return Result.Success<IStateLockHandle>(NullStateLockHandle.Instance);
        }

        if (arguments.SkipLock)
        {
            // Peek so the warning is honest: name the lock we are running past rather than ignoring it silently.
            var peeked = await stateLock.Peek(cancellationToken);
            var held = peeked.Value?.Held;

            return Result.From<IStateLockHandle>(NullStateLockHandle.Instance, [LockDiagnostics.RunningUnlocked(arguments.Operation, held)]);
        }

        try
        {
            var createdUtc = DateTimeOffset.UtcNow;
            var lockInfo = new StateLockInfo(
                Id: LockId.New(),
                Operation: arguments.Operation,
                Who: LockHolder.Current(),
                CreatedUtc: createdUtc,
                ExpiresUtc: arguments.TimeToLive is { } ttl ? createdUtc + ttl : null);
            return await stateLock.Acquire(lockInfo, cancellationToken);
        }
        catch (StateLockedException ex)
        {
            // Contention keeps a typed signal because it carries the holder's details; the engine owns the wording,
            // including the --no-lock hint, so no backend has to re-author it.
            return Result.Failure<IStateLockHandle>(LockDiagnostics.StateLocked(arguments.Operation, ex));
        }
    }

    public async Task<Result<LockReleaseResult>> Release(CancellationToken cancellationToken = default)
    {
        // Nothing to release when the state is unlockable.
        if (stateLock is null)
        {
            return new LockReleaseResult(null);
        }

        // Capture what is held so the caller can report it, then force-release. A null peek means the state was
        // already free — there is nothing to remove.
        var peeked = await stateLock.Peek(cancellationToken);
        if (peeked.IsFailure)
        {
            return Result.Failure<LockReleaseResult>(peeked.Diagnostics);
        }

        if (peeked.Require().Held is not { } held)
        {
            return new LockReleaseResult(null);
        }

        var released = await stateLock.Release(cancellationToken);

        return released.IsFailure
            ? Result.Failure<LockReleaseResult>(released.Diagnostics)
            : new LockReleaseResult(held);
    }
}
