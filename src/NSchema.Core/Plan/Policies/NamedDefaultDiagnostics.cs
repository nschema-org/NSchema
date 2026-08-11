namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="NamedDefaultPolicy"/>.
/// </summary>
internal static class NamedDefaultDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Capability;

    /// <summary>
    /// A named default declared against an engine that does not name defaults.
    /// </summary>
    public static Diagnostic NamedDefaultNotSupported(string sites) =>
        Diagnostic.Warning(Source, "named-default-not-supported",
            $"This engine does not name column defaults, so the constraint name on {sites:text} is not applied. The default itself is unaffected.");
}
