namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="ReferentialActionPolicy"/>.
/// </summary>
internal static class ReferentialActionDiagnostics
{
    internal static readonly DiagnosticSource Source = "referential-actions";

    /// <summary>
    /// <c>RESTRICT</c> declared against an engine that has only <c>NO ACTION</c>.
    /// </summary>
    public static Diagnostic RestrictNotSupported(string sites) =>
        Diagnostic.Warning(Source, "restrict-not-supported",
            $"This engine has no RESTRICT referential action, so {sites:text} will use NO ACTION instead.");
}
