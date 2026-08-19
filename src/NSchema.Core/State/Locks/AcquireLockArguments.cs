namespace NSchema.State.Locks;

/// <summary>
/// Arguments for taking the state lock.
/// </summary>
/// <param name="Operation">The name of the operation acquiring the lock (e.g. <c>"apply"</c>, <c>"refresh"</c>).</param>
public sealed record AcquireLockArguments(string Operation)
{
    /// <summary>
    /// An optional lifetime after which the lock is considered stale.
    /// </summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>
    /// When <see langword="true"/>, runs without acquiring.
    /// </summary>
    public bool SkipLock { get; init; }
}
