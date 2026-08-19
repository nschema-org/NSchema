namespace NSchema.State.Locks;

/// <summary>
/// The diagnostics minted by the lock manager.
/// </summary>
internal static class LockDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Lock;

    /// <summary>
    /// A deliberate skip-lock run; names the held lock it is running past when there is one.
    /// </summary>
    public static Diagnostic RunningUnlocked(string operation, StateLockInfo? held) => held is null
        ? Diagnostic.Warning(Source, "running-unlocked",
            $"Running {operation} without the state lock; make sure no other operation runs against this state at the same time.")
        : Diagnostic.Warning(Source, "running-unlocked",
            $"Running {operation} without the state lock; the state is currently locked by {held.Who} (operation '{held.Operation}', since {held.CreatedUtc:u}) — proceeding anyway.");

    /// <summary>
    /// The lock backend could not be reached to take the lock.
    /// </summary>
    public static Diagnostic Unreachable(string operation, Exception exception) =>
        Diagnostic.Error(Source, "lock-unreachable",
            $"Could not take the state lock for {operation}: {ExceptionMessage.Describe(exception):text}");

    /// <summary>
    /// The state lock is already held; carries the holder's details when readable.
    /// </summary>
    public static Diagnostic StateLocked(string operation, StateLockedException exception) => exception.ExistingLock is { } held
        ? Diagnostic.Error(Source, "state-locked",
            $"Cannot run {operation}: the state is locked by {held.Who} (operation '{held.Operation}', since {held.CreatedUtc:u}). Wait for it to finish, or re-run with --no-lock to proceed anyway.")
        : Diagnostic.Error(Source, "state-locked", exception.Message);
}
