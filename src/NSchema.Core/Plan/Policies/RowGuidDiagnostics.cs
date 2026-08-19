namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="RowGuidPolicy"/>.
/// </summary>
internal static class RowGuidDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Capability;

    /// <summary>
    /// A row-guid column declared against an engine that has no such marker.
    /// </summary>
    public static Diagnostic RowGuidNotSupported(string sites) =>
        Diagnostic.Warning(Source, "row-guid-not-supported",
            $"This engine has no row-guid column marker, so {sites:text} will be created as an ordinary column.");
}
