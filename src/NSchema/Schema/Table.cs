using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents a database table.
/// </summary>
/// <param name="Name">The name of the table.</param>
/// <param name="PreviousName">The previous name of the table, if it has been renamed.</param>
/// <param name="PrimaryKey">The primary key of the table.</param>
/// <param name="Comment">An optional comment or description for the table.</param>
/// <param name="Columns">A list of columns that are part of the table.</param>
/// <param name="ForeignKeys">A list of foreign keys that define the relationships between this table and other tables in the database schema.</param>
/// <param name="Indexes">A list of indexes that are defined on the table.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the table.</param>
[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public record Table(
    string Name,
    string? PreviousName = null,
    PrimaryKey? PrimaryKey = null,
    string? Comment = null,
    IReadOnlyList<Column>? Columns = null,
    IReadOnlyList<ForeignKey>? ForeignKeys = null,
    IReadOnlyList<TableIndex>? Indexes = null,
    IReadOnlyList<TableGrant>? Grants = null
)
{
    /// <summary>
    /// Gets the columns that are part of the table.
    /// </summary>
    public IReadOnlyList<Column> Columns { get; init; } = Columns ?? [];

    /// <summary>
    /// Gets the foreign keys that define the relationships between this table and other tables in the database schema.
    /// </summary>
    public IReadOnlyList<ForeignKey> ForeignKeys { get; init; } = ForeignKeys ?? [];

    /// <summary>
    /// Gets the indexes that are defined on the table.
    /// </summary>
    public IReadOnlyList<TableIndex> Indexes { get; init; } = Indexes ?? [];

    /// <summary>
    /// Gets the grants that define the permissions associated with the table.
    /// </summary>
    public IReadOnlyList<TableGrant> Grants { get; init; } = Grants ?? [];
}
