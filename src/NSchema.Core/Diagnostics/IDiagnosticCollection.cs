namespace NSchema.Diagnostics;

/// <summary>
/// A read-only view over an ordered set of diagnostics.
/// </summary>
/// <typeparam name="TDiagnostic">The diagnostic type the producer mints.</typeparam>
public interface IDiagnosticCollection<out TDiagnostic> : IReadOnlyList<TDiagnostic> where TDiagnostic : Diagnostic
{
    /// <summary>
    /// Whether any finding is an error.
    /// </summary>
    bool HasErrors { get; }

    /// <summary>
    /// The error-severity subset.
    /// </summary>
    IEnumerable<TDiagnostic> Errors { get; }

    /// <summary>
    /// The warning-severity subset.
    /// </summary>
    IEnumerable<TDiagnostic> Warnings { get; }
}
