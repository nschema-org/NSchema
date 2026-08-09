namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="NotForReplicationPolicy"/>.
/// </summary>
internal static class NotForReplicationDiagnostics
{
    internal static readonly DiagnosticSource Source = "replication";

    /// <summary>
    /// <c>NOT FOR REPLICATION</c> declared against an engine with no such concept.
    /// </summary>
    public static Diagnostic NotForReplicationNotSupported(string sites) =>
        Diagnostic.Warning(Source, "not-for-replication-not-supported",
            $"This engine has no NOT FOR REPLICATION, so {sites:text} will behave the same way for every writer.");
}
