namespace NSchema.Operations;

/// <summary>
/// The result of running diagnostics.
/// </summary>
/// <param name="Checks">Every diagnostic produced by the infrastructure probes, one per check.</param>
public sealed record DoctorResult(IDiagnosticCollection<Diagnostic> Checks)
{
    /// <summary>
    /// The error-severity checks; the configured infrastructure is unhealthy when any are present.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Checks.Errors;

    /// <summary>
    /// Whether any check failed (an error-severity probe result).
    /// </summary>
    public bool HasErrors => Checks.HasErrors;
}
