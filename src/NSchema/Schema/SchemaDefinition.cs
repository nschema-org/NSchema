using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents the definition of a database schema.
/// </summary>
/// <param name="Name">The name of the schema.</param>
/// <param name="OldName">The previous name of the schema, if it has been renamed.</param>
/// <param name="IsPartial">Indicates whether the schema definition is partial, meaning it may not include all details of the schema.</param>
/// <param name="Comment">An optional comment or description for the schema.</param>
/// <param name="Tables">A list of tables that are part of the schema.</param>
/// <param name="DroppedTables">A list of tables that have been dropped from the schema.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the schema.</param>
[DebuggerDisplay("{Name,nq} ({Tables.Count} tables)")]
public record SchemaDefinition(
    string Name,
    string? OldName,
    bool IsPartial,
    string? Comment,
    IReadOnlyList<Table> Tables,
    IReadOnlyList<string> DroppedTables,
    IReadOnlyList<SchemaGrant> Grants
)
{
    /// <summary>
    /// Creates a new <see cref="SchemaDefinition"/> with the given options, defaulting unspecified members.
    /// </summary>
    /// <param name="name">The name of the schema.</param>
    /// <param name="oldName">The previous name of the schema, if it has been renamed.</param>
    /// <param name="isPartial">Indicates whether the schema definition is partial, meaning it may not include all details of the schema.</param>
    /// <param name="comment">An optional comment or description for the schema.</param>
    /// <param name="tables">A list of tables that are part of the schema.</param>
    /// <param name="droppedTables">A list of tables that have been dropped from the schema.</param>
    /// <param name="grants">A list of grants that define the permissions associated with the schema.</param>
    public static SchemaDefinition Create(
        string name,
        string? oldName = null,
        bool isPartial = false,
        string? comment = null,
        IReadOnlyList<Table>? tables = null,
        IReadOnlyList<string>? droppedTables = null,
        IReadOnlyList<SchemaGrant>? grants = null
    ) => new(name, oldName, isPartial, comment, tables ?? [], droppedTables ?? [], grants ?? []);
}
