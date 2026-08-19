namespace NSchema.State.Locks;

/// <summary>
/// Thrown when a state lock cannot be acquired because it is already held by another operation.
/// </summary>
/// <param name="message">A description of the failure.</param>
/// <param name="existingLock">Information about the held lock, if available.</param>
public sealed class StateLockedException(string message, StateLockInfo? existingLock = null) : Exception(message)
{
    /// <summary>
    /// Information about the lock currently held, when the implementation could read it; otherwise <see langword="null"/>.
    /// </summary>
    public StateLockInfo? ExistingLock { get; } = existingLock;
}
