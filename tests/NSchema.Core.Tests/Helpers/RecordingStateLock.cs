using NSchema.State.Locks;
using NSchema.State.Locks.Plugins;

namespace NSchema.Tests.Helpers;

/// <summary>
/// An <see cref="IStateLock"/> test double that records each acquisition and how many handles were released.
/// Set <see cref="OnAcquire"/> to simulate a contended lock by throwing <see cref="StateLockedException"/>.
/// </summary>
internal sealed class RecordingStateLock : IStateLock
{
    public List<StateLockInfo> Acquisitions { get; } = [];
    public int Released { get; private set; }
    public int ForceReleases { get; private set; }
    public int Peeks { get; private set; }
    public Func<StateLockInfo, Task>? OnAcquire { get; set; }

    /// <summary>The value returned from <see cref="Peek"/> (defaults to nothing held).</summary>
    public StateLockInfo? PeekResult { get; set; }

    /// <summary>Overrides <see cref="Peek"/>, so a test can vary what is held.</summary>
    public Func<StateLockInfo?>? OnPeek { get; set; }

    /// <summary>When set, <see cref="Peek"/> reports this failure instead of returning a lock.</summary>
    public Diagnostic? PeekFailure { get; set; }

    /// <summary>When set, <see cref="Acquire"/> reports this failure instead of taking the lock.</summary>
    public Diagnostic? AcquireFailure { get; set; }

    public Task<Result<LockPeekResult>> Peek(CancellationToken cancellationToken = default)
    {
        Peeks++;

        return Task.FromResult(PeekFailure is { } failure
            ? Result.Failure<LockPeekResult>(failure)
            : Result.Success(new LockPeekResult(OnPeek is null ? PeekResult : OnPeek())));
    }

    public async Task<Result<IStateLockHandle>> Acquire(StateLockInfo lockInfo, CancellationToken cancellationToken = default)
    {
        if (OnAcquire is not null)
        {
            await OnAcquire(lockInfo);
        }

        if (AcquireFailure is { } failure)
        {
            return Result.Failure<IStateLockHandle>(failure);
        }

        Acquisitions.Add(lockInfo);
        return Result.Success<IStateLockHandle>(new Handle(this, lockInfo));
    }

    public ValueTask<Result> Release(CancellationToken cancellationToken = default)
    {
        ForceReleases++;
        return ValueTask.FromResult(Result.Success());
    }

    private sealed class Handle(RecordingStateLock owner, StateLockInfo info) : IStateLockHandle
    {
        public StateLockInfo Info => info;

        public ValueTask<Result> Release(CancellationToken cancellationToken = default)
        {
            owner.Released++;
            return ValueTask.FromResult(Result.Success());
        }
    }
}
