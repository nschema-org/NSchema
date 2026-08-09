namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="ClusteringPolicy"/>.
/// </summary>
internal static class ClusteringDiagnostics
{
    internal static readonly DiagnosticSource Source = "clustering";

    /// <summary>
    /// Clustering declared against an engine that has no such concept.
    /// </summary>
    public static Diagnostic ClusteringNotSupported(string sites) =>
        Diagnostic.Warning(Source, "clustering-not-supported",
            $"This engine does not support clustered indexes, so the clustering declared on {sites:text} is not applied.");
}
