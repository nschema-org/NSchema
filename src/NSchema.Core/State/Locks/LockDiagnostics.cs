namespace NSchema.State.Locks;

/// <summary>
/// The diagnostics minted by the lock manager. The source is the requesting operation's name.
/// </summary>
internal static class LockDiagnostics
{
    /// <summary>
    /// A deliberate skip-lock run; names the held lock it is running past when there is one.
    /// </summary>
    public static Diagnostic RunningUnlocked(string operation, StateLockInfo? held) => held is null
        ? Diagnostic.Warning(operation, "Running without the state lock; make sure no other operation runs against this state at the same time.")
        : Diagnostic.Warning(operation, $"Running without the state lock; the state is currently locked by {held.Who} (operation '{held.Operation}', since {held.CreatedUtc:u}) — proceeding anyway.");

    /// <summary>
    /// The lock backend could not be reached to take the lock.
    /// </summary>
    public static Diagnostic Unreachable(string operation, Exception exception) =>
        Diagnostic.Error(operation, $"Could not take the state lock: {ExceptionMessage.Describe(exception):text}");

    /// <summary>
    /// The state lock is already held; carries the holder's details when readable.
    /// </summary>
    public static Diagnostic StateLocked(string operation, StateLockedException exception) => exception.ExistingLock is { } held
        ? Diagnostic.Error(operation, $"The state is locked by {held.Who} (operation '{held.Operation}', since {held.CreatedUtc:u}). Wait for it to finish, or re-run with --no-lock to proceed anyway.")
        : Diagnostic.Error(operation, exception.Message);
}
