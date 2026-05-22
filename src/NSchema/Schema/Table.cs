using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents a database table.
/// </summary>
/// <param name="Name">The name of the table.</param>
/// <param name="OldName">The previous name of the table, if it has been renamed.</param>
/// <param name="PrimaryKey">The primary key of the table.</param>
/// <param name="Comment">An optional comment or description for the table.</param>
/// <param name="Columns">A list of columns that are part of the table.</param>
/// <param name="ForeignKeys">A list of foreign keys that define the relationships between this table and other tables in the database schema.</param>
/// <param name="Indexes">A list of indexes that are defined on the table.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the table.</param>
[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public record Table(
    string Name,
    string? OldName,
    PrimaryKey? PrimaryKey,
    string? Comment,
    IReadOnlyList<Column> Columns,
    IReadOnlyList<ForeignKey> ForeignKeys,
    IReadOnlyList<TableIndex> Indexes,
    IReadOnlyList<TableGrant> Grants
)
{
    /// <summary>
    /// Creates a new <see cref="Table"/> with the given options, defaulting unspecified members.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    /// <param name="oldName">The previous name of the table, if it has been renamed.</param>
    /// <param name="primaryKey">The primary key of the table.</param>
    /// <param name="comment">An optional comment or description for the table.</param>
    /// <param name="columns">A list of columns that are part of the table.</param>
    /// <param name="foreignKeys">A list of foreign keys that define the relationships between this table and other tables in the database schema.</param>
    /// <param name="indexes">A list of indexes that are defined on the table.</param>
    /// <param name="grants">A list of grants that define the permissions associated with the table.</param>
    public static Table Create(
        string name,
        string? oldName = null,
        PrimaryKey? primaryKey = null,
        string? comment = null,
        IReadOnlyList<Column>? columns = null,
        IReadOnlyList<ForeignKey>? foreignKeys = null,
        IReadOnlyList<TableIndex>? indexes = null,
        IReadOnlyList<TableGrant>? grants = null
    ) => new(name, oldName, primaryKey, comment, columns ?? [], foreignKeys ?? [], indexes ?? [], grants ?? []);
}
