using NSchema.Model;

namespace NSchema.Project.Policies;

/// <summary>
/// The diagnostics minted by <see cref="ReplicationPolicy"/>.
/// </summary>
internal static class ReplicationDiagnostics
{
    internal static readonly DiagnosticSource Source = DiagnosticSources.Replication;

    /// <summary>
    /// A table published for updates or deletions with nothing to identify its rows by.
    /// </summary>
    public static Diagnostic UnidentifiedTable(SqlIdentifier publication, ObjectAddress table) =>
        Diagnostic.Warning(Source, "unidentified-published-table",
            $"Publication '{publication}' publishes updates or deletes of '{table}', which has no primary key or replica identity to identify its rows, so its updates and deletes will be rejected.");

    /// <summary>
    /// A row filter reading columns the table's row identity does not include.
    /// </summary>
    public static Diagnostic FilterOutsideIdentity(SqlIdentifier publication, ObjectAddress table, IEnumerable<SqlIdentifier> columns) =>
        Diagnostic.Warning(Source, "filter-outside-identity",
            $"Publication '{publication}' filters '{table}' on {string.Join(", ", columns.Select(c => $"'{c}'")):text}, which its row identity does not include, so its updates and deletes may be rejected.");
}
