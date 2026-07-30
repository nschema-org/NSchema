namespace NSchema.Diagnostics;

/// <summary>
/// How the engine treats the findings its policies report.
/// </summary>
public sealed class DiagnosticOptions
{
    /// <summary>
    /// How individual findings are enforced, by code.
    /// </summary>
    public Dictionary<DiagnosticCode, PolicyEnforcement> ByCode { get; } = [];

    /// <summary>
    /// How every finding from a producer is enforced, by source.
    /// </summary>
    public Dictionary<DiagnosticSource, PolicyEnforcement> BySource { get; } = [];

    /// <summary>
    /// Applies the configured enforcement, dropping what is not reported at all.
    /// </summary>
    /// <param name="diagnostics">The findings as their producers reported them.</param>
    public IEnumerable<Diagnostic> Apply(IEnumerable<Diagnostic> diagnostics)
    {
        // Configuration that cannot be honoured is said out loud rather than quietly dropped — but once per
        // code, however many times the finding it names occurs.
        var refused = new HashSet<DiagnosticCode>();

        foreach (var diagnostic in diagnostics)
        {
            if (Enforcement(diagnostic) is not { } enforcement)
            {
                yield return diagnostic;
                continue;
            }

            var severity = enforcement.Severity();
            if (Lowers(diagnostic, severity) && !CanBeLowered(diagnostic))
            {
                yield return diagnostic;
                if (refused.Add(diagnostic.Code))
                {
                    yield return EnforcementDiagnostics.CannotBeLowered(diagnostic.Code);
                }
            }
            else if (severity is { } applied)
            {
                yield return diagnostic with { Severity = applied };
            }
        }
    }

    // Reporting nothing at all is the furthest a finding can be lowered.
    private static bool Lowers(Diagnostic diagnostic, DiagnosticSeverity? severity) =>
        severity is null || severity < diagnostic.Severity;

    private static bool CanBeLowered(Diagnostic diagnostic) => diagnostic.Kind == DiagnosticKind.Advisory;

    private PolicyEnforcement? Enforcement(Diagnostic diagnostic) =>
        ByCode.TryGetValue(diagnostic.Code, out var byCode) ? byCode
        : BySource.TryGetValue(diagnostic.Source, out var bySource) ? bySource
        : null;
}
