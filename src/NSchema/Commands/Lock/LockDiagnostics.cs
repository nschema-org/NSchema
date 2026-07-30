using NSchema.State.Locks;

namespace NSchema.Commands.Lock;

/// <summary>
/// The diagnostics the CLI mints while managing the state lock.
/// </summary>
/// <remarks>
/// The source is shared with the engine's own locking findings, so configuring or grouping by source covers the
/// whole subsystem rather than only the half that happened to report.
/// </remarks>
internal static class LockDiagnostics
{
    internal static readonly DiagnosticSource Source = "lock";

    /// <summary>
    /// A release whose given id no longer matches the held lock.
    /// </summary>
    public static Diagnostic IdMismatch(string lockId, StateLockInfo current) =>
        Diagnostic.Error(Source, "lock-id-mismatch",
            $"The lock id '{lockId}' does not match the held lock '{current.Id.Value}' (held by {current.Who}, "
            + $"operation '{current.Operation}'). Check the current lock with 'nschema lock status'.");
}
