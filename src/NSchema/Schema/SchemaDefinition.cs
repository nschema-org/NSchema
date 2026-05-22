using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents the definition of a database schema.
/// </summary>
/// <param name="Name">The name of the schema.</param>
/// <param name="PreviousName">The previous name of the schema, if it has been renamed.</param>
/// <param name="IsPartial">Indicates whether the schema definition is partial, meaning it may not include all details of the schema.</param>
/// <param name="Comment">An optional comment or description for the schema.</param>
/// <param name="Tables">A list of tables that are part of the schema.</param>
/// <param name="DroppedTables">A list of tables that have been dropped from the schema.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the schema.</param>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public record SchemaDefinition(
    string Name,
    string? PreviousName = null,
    bool IsPartial = false,
    string? Comment = null,
    IReadOnlyList<Table>? Tables = null,
    IReadOnlyList<string>? DroppedTables = null,
    IReadOnlyList<SchemaGrant>? Grants = null
)
{
    /// <summary>
    /// Gets the tables that are part of the schema.
    /// </summary>
    public IReadOnlyList<Table> Tables { get; init; } = Tables ?? [];

    /// <summary>
    /// Gets the names of the tables that have been dropped from the schema.
    /// </summary>
    public IReadOnlyList<string> DroppedTables { get; init; } = DroppedTables ?? [];

    /// <summary>
    /// Gets the grants that define the permissions associated with the schema.
    /// </summary>
    public IReadOnlyList<SchemaGrant> Grants { get; init; } = Grants ?? [];
}
