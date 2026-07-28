namespace NSchema.State.Locks;

/// <summary>
/// The outcome of reading the lock held against the state.
/// </summary>
/// <param name="Held">The lock currently held, or <see langword="null"/> when the state is free or unlockable.</param>
public sealed record LockPeekResult(StateLockInfo? Held);
