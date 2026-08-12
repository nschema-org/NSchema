namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="CommentStoragePolicy"/>.
/// </summary>
internal static class CommentStorageDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Comments;

    /// <summary>
    /// Comments declared against an engine that records none.
    /// </summary>
    public static Diagnostic NotSupported(string sites) =>
        Diagnostic.Warning(Source, "comments-not-supported",
            $"This engine does not record comments, so {sites:text} cannot be documented.");
}
