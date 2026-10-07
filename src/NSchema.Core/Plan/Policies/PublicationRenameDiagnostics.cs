using NSchema.Model;

namespace NSchema.Plan.Policies;

/// <summary>
/// The diagnostics minted by <see cref="PublicationRenamePolicy"/>.
/// </summary>
internal static class PublicationRenameDiagnostics
{
    private static readonly DiagnosticSource Source = DiagnosticSources.Replication;

    /// <summary>
    /// A renamed publication, which its subscribers still name.
    /// </summary>
    public static Diagnostic Renamed(SqlIdentifier from, SqlIdentifier to) =>
        Diagnostic.Warning(Source, "publication-renamed",
            $"Publication '{from}' is renamed to '{to}'. Make sure its subscribers are updated separately.");
}
