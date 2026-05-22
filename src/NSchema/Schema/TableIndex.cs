using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents an index on a table within the database schema.
/// </summary>
/// <param name="Name">The name of the index.</param>
/// <param name="ColumnNames">A list of column names that are included in the index.</param>
/// <param name="IsUnique">A boolean value indicating whether the index enforces uniqueness on the indexed columns.</param>
/// <param name="Comment">An optional comment or description for the index.</param>
/// <param name="Predicate">An optional predicate that defines a partial index.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record TableIndex(string Name, IReadOnlyList<string> ColumnNames, bool IsUnique = false, string? Comment = null, string? Predicate = null)
{
    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", ColumnNames)})" +
        (IsUnique ? " UNIQUE" : "") +
        (Predicate is { } p ? $" WHERE {p}" : "");

    /// <summary>
    /// Determines whether the specified TableIndex is equal to the current TableIndex.
    /// </summary>
    /// <param name="other">The TableIndex to compare with the current TableIndex.</param>
    /// <returns>true if the specified TableIndex is equal to the current TableIndex; otherwise, false.</returns>
    public virtual bool Equals(TableIndex? other) =>
        other != null
        && Name == other.Name
        && IsUnique == other.IsUnique
        && ColumnNames.SequenceEqual(other.ColumnNames)
        && Predicate == other.Predicate;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames, IsUnique, Predicate);
}
