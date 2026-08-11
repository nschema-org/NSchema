namespace NSchema.Operations.Workflow;

/// <summary>
/// The diagnostics minted by the migration workflow.
/// </summary>
internal static class WorkflowDiagnostics
{
    internal static readonly DiagnosticSource PlanSource = DiagnosticSources.Plan;

    internal static readonly DiagnosticSource RefreshSource = DiagnosticSources.Refresh;

    internal static readonly DiagnosticSource StateSource = DiagnosticSources.State;

    /// <summary>
    /// Planning without a configured state store (the ledger is half of the current state).
    /// </summary>
    public static Diagnostic StoreRequiredForPlanning => Diagnostic.Error(PlanSource, "state-store-required-for-plan",
        "Planning requires a state store to read the recorded state and the run-once ledger. Register one, or declare the database disposable with ephemeral state.");

    /// <summary>
    /// Refreshing without a configured state store (nothing to capture to).
    /// </summary>
    public static Diagnostic StoreRequiredForRefresh => Diagnostic.Error(RefreshSource, "state-store-required-for-refresh",
        "Unable to refresh state without a configured state store.");

    /// <summary>
    /// An unreadable existing payload was left in place because the refresh was not forced.
    /// </summary>
    public static Diagnostic StateNotReplaced => Diagnostic.Error(StateSource, "state-not-replaced",
        "The existing state payload was not replaced. Repair it with state pull/push, or re-run the " +
        "refresh with force to replace it and reset the run-once script ledger.");

    /// <summary>
    /// A forced refresh replaced an unreadable payload, resetting the run-once ledger.
    /// </summary>
    public static Diagnostic LedgerReset => Diagnostic.Warning(StateSource, "ledger-reset",
        "The existing state payload could not be read and has been replaced; the run-once script ledger was " +
        "reset. Untaint any run-once scripts that have already run, or they will run again on the next apply.");
}
