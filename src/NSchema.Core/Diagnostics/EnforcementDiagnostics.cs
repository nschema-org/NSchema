namespace NSchema.Diagnostics;

/// <summary>
/// The diagnostics minted when applying configured enforcement.
/// </summary>
internal static class EnforcementDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Enforcement;

    /// <summary>
    /// A finding configured to be reported more leniently than it can be.
    /// </summary>
    public static Diagnostic CannotBeLowered(DiagnosticCode code) =>
        Diagnostic.Warning(Source, "cannot-be-lowered",
            $"The configured enforcement for '{code}' is not applied: it reports that NSchema cannot do what was asked, so silencing it would produce something wrong rather than something permitted.");
}
