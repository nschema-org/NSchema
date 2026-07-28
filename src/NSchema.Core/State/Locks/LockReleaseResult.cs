namespace NSchema.State.Locks;

/// <summary>
/// The outcome of force-releasing the state lock.
/// </summary>
/// <param name="Released">The lock that was released, or <see langword="null"/> when the state was already free or unlockable.</param>
public sealed record LockReleaseResult(StateLockInfo? Released);
