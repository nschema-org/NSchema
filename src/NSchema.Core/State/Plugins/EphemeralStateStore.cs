using NSchema.State.Locks;
using NSchema.State.Locks.Plugins;

namespace NSchema.State.Plugins;

/// <summary>
/// An in-memory state backend for disposable databases.
/// </summary>
internal sealed class EphemeralStateStore : IDatabaseStateStore, IStateLock
{
    private readonly Lock _gate = new();
    private ReadOnlyMemory<byte>? _payload;
    private StateLockInfo? _held;

    // In-memory: nothing here can be unreachable, so every outcome is a success.
    public Task<Result<StoreReadResult>> Read(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Result.Success(new StoreReadResult(_payload)));
        }
    }

    public Task<Result> Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            // Copy: the caller's buffer must not be able to mutate the stored payload after the write.
            _payload = state.ToArray();
        }
        return Task.FromResult(Result.Success());
    }

    public Task<Result<IStateLockHandle>> Acquire(StateLockInfo lockInfo, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_held is { } holder)
            {
                throw new StateLockedException(
                    $"The state is locked by {holder.Who} (operation '{holder.Operation}', since {holder.CreatedUtc:u}).",
                    holder);
            }

            _held = lockInfo;
            return Task.FromResult(Result.Success<IStateLockHandle>(new Handle(this, _held)));
        }
    }

    public Task<Result<LockPeekResult>> Peek(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Result.Success(new LockPeekResult(_held)));
        }
    }

    public ValueTask<Result> Release(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _held = null;
        }
        return ValueTask.FromResult(Result.Success());
    }

    private sealed class Handle(EphemeralStateStore owner, StateLockInfo info) : IStateLockHandle
    {
        public StateLockInfo Info => info;

        public ValueTask<Result> Release(CancellationToken cancellationToken = default)
        {
            lock (owner._gate)
            {
                // Only clear a hold this handle owns; a force-released and re-acquired lock is someone else's.
                if (ReferenceEquals(owner._held, info))
                {
                    owner._held = null;
                }
            }
            return ValueTask.FromResult(Result.Success());
        }
    }
}
