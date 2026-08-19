using NSchema.Operations.Progress;
using NSchema.State.Locks;

namespace NSchema.Operations;

/// <summary>
/// The diagnostics minted by <see cref="DoctorOperation"/> — one per health check.
/// </summary>
/// <remarks>
/// Every check shares one source, so a user configures the health report as a whole; the piece of infrastructure a
/// check probes is carried by its code, which the runtime's own findings about that infrastructure do not share.
/// </remarks>
internal static class DoctorDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Doctor;

    /// <summary>
    /// No database is configured, so the application is planning offline.
    /// </summary>
    public static Diagnostic DatabaseNotConfigured =>
        Diagnostic.Info(Source, "database-not-configured", "Database connection not available.");

    /// <summary>
    /// The database could not be introspected.
    /// </summary>
    public static Diagnostic DatabaseUnreachable(string reason) =>
        Diagnostic.Error(Source, "database-unreachable", $"Unable to reach the database: {reason:text}");

    /// <summary>
    /// The database was introspected end to end.
    /// </summary>
    public static Diagnostic DatabaseConnected(int schemas) =>
        Diagnostic.Info(Source, "database-connected", $"Database connected ({StatusHelpers.Count(schemas, "schema")} visible).");

    /// <summary>
    /// No state store is configured, so nothing can be planned or applied.
    /// </summary>
    public static Diagnostic StateStoreNotConfigured =>
        Diagnostic.Error(Source, "state-store-not-configured", "State store not configured.");

    /// <summary>
    /// The state store could not be read.
    /// </summary>
    public static Diagnostic StateStoreUnreachable(string reason) =>
        Diagnostic.Error(Source, "state-store-unreachable", $"Unable to reach the state store: {reason:text}");

    /// <summary>
    /// The state store is reachable but holds no snapshot yet.
    /// </summary>
    public static Diagnostic StateStoreEmpty =>
        Diagnostic.Info(Source, "state-store-empty", "State store is empty (no state recorded yet).");

    /// <summary>
    /// The recorded snapshot round-tripped through the serializer.
    /// </summary>
    public static Diagnostic StateStoreValid =>
        Diagnostic.Info(Source, "state-store-valid", "The recorded state is valid.");

    /// <summary>
    /// The recorded snapshot could not be deserialized, which would break every plan.
    /// </summary>
    public static Diagnostic StateStoreUnreadable(string reason) =>
        Diagnostic.Error(Source, "state-store-unreadable", $"The recorded state is unreadable: {reason:text}");

    /// <summary>
    /// The state lock could not be probed.
    /// </summary>
    public static Diagnostic StateLockUncheckable(string reason) =>
        Diagnostic.Error(Source, "state-lock-uncheckable", $"Unable to check state lock: {reason:text}");

    /// <summary>
    /// The state lock is free.
    /// </summary>
    public static Diagnostic StateLockFree =>
        Diagnostic.Info(Source, "state-lock-free", "State lock is not locked.");

    /// <summary>
    /// The state lock is held, which may be a legitimately-running operation rather than a misconfiguration.
    /// </summary>
    public static Diagnostic StateLockHeld(StateLockInfo info) =>
        Diagnostic.Warning(Source, "state-lock-held", $"State is locked by {info.Who} (operation '{info.Operation}', since {info.CreatedUtc:u}).");
}
