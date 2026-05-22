using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents a foreign key constraint in a database schema.
/// </summary>
/// <param name="Name">The name of the foreign key constraint.</param>
/// <param name="ColumnNames">A list of column names in the current table that are part of the foreign key constraint.</param>
/// <param name="ReferencedSchema">The name of the schema that contains the referenced table.</param>
/// <param name="ReferencedTable">The name of the table that is referenced by the foreign key constraint.</param>
/// <param name="ReferencedColumnNames">A list of column names in the referenced table that are part of the foreign key constraint.</param>
/// <param name="OnDelete">The referential action to be taken when a referenced row is deleted (e.g., CASCADE, SET NULL, NO ACTION).</param>
/// <param name="OnUpdate">The referential action to be taken when a referenced row is updated (e.g., CASCADE, SET NULL, NO ACTION).</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record ForeignKey(
    string Name,
    IReadOnlyList<string> ColumnNames,
    string ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumnNames,
    ReferentialAction OnDelete,
    ReferentialAction OnUpdate
)
{
    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", ColumnNames)}) -> {ReferencedSchema}.{ReferencedTable} ({string.Join(", ", ReferencedColumnNames)})";

    /// <summary>
    /// Determines whether the specified ForeignKey is equal to the current ForeignKey.
    /// </summary>
    /// <param name="other">The ForeignKey to compare with the current ForeignKey.</param>
    /// <returns>true if the specified ForeignKey is equal to the current ForeignKey; otherwise, false.</returns>
    public virtual bool Equals(ForeignKey? other) =>
        other != null
        && Name == other.Name
        && ColumnNames.SequenceEqual(other.ColumnNames)
        && ReferencedSchema == other.ReferencedSchema
        && ReferencedTable == other.ReferencedTable
        && ReferencedColumnNames.SequenceEqual(other.ReferencedColumnNames)
        && OnDelete == other.OnDelete
        && OnUpdate == other.OnUpdate;

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Name, ColumnNames, ReferencedSchema, ReferencedTable, ReferencedColumnNames, OnDelete, OnUpdate);
}
