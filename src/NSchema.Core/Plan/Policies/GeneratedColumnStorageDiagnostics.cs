namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="GeneratedColumnStoragePolicy"/>.
/// </summary>
internal static class GeneratedColumnStorageDiagnostics
{
    internal static readonly DiagnosticSource Source = "generated-columns";

    /// <summary>
    /// A virtual generated column declared against an engine that only stores them.
    /// </summary>
    public static Diagnostic VirtualNotSupported(string sites) =>
        Diagnostic.Warning(Source, "virtual-generated-column-not-supported",
            $"This engine stores every generated column, so {sites:text} will be stored rather than computed on read.");
}
