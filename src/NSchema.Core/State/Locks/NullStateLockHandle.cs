namespace NSchema.State.Locks;

/// <summary>
/// A null-object <see cref="IStateLockHandle"/> for the cases where no real lock is held.
/// </summary>
internal sealed class NullStateLockHandle : IStateLockHandle
{
    /// <summary>
    /// The shared instance — the handle is stateless.
    /// </summary>
    public static readonly NullStateLockHandle Instance = new();

    private NullStateLockHandle()
    {
    }

    /// <summary>
    /// No lock is held, so there is no lock metadata to report.
    /// </summary>
    public StateLockInfo Info => throw new NotSupportedException("No lock is held; this handle represents an unlocked run.");

    public ValueTask<Result> Release(CancellationToken cancellationToken = default) => ValueTask.FromResult(Result.Success());
}
