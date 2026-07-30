namespace NSchema.Operations;

/// <summary>
/// The diagnostics minted by the apply operation.
/// </summary>
internal static class ApplyDiagnostics
{
    internal static readonly DiagnosticSource Source = "apply";

    /// <summary>
    /// Applying without a configured state store (the run-once ledger could not be recorded).
    /// </summary>
    public static Diagnostic StoreRequired => Diagnostic.Error(Source, "store-required",
        "Applying a plan requires a state store to record the run. Register one, or declare the database disposable with ephemeral state.");

    /// <summary>
    /// The plan's carried diff failed the policies at the point of execution.
    /// </summary>
    public static Diagnostic BlockedByPolicy => Diagnostic.Error(Source, "blocked-by-policy",
        "The plan is blocked by policy and was not applied. Re-run with force to apply it anyway.");

    /// <summary>
    /// Applying without a registered SQL executor.
    /// </summary>
    public static Diagnostic MissingExecutor => Diagnostic.Error(Source, "missing-executor",
        "Applying a plan requires a database provider to execute SQL, but none is registered.");

    /// <summary>
    /// A migration that failed partway through execution; the database may be partially migrated.
    /// </summary>
    public static Diagnostic ExecutionFailed(Exception exception) => Diagnostic.Error(Source, "execution-failed",
        $"The migration failed and may have been applied only in part: {ExceptionMessage.Describe(exception):text}");
}
