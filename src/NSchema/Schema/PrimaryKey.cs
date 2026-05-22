using System.Diagnostics;

namespace NSchema.Schema;

/// <summary>
/// Represents a primary key constraint in a database schema.
/// </summary>
/// <param name="Name">The name of the primary key constraint.</param>
/// <param name="ColumnNames">A list of column names that are part of the primary key constraint.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record PrimaryKey(string Name, IReadOnlyList<string> ColumnNames)
{
    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})";

    /// <summary>
    /// Determines whether the specified PrimaryKey is equal to the current PrimaryKey.
    /// </summary>
    /// <param name="other">The PrimaryKey to compare with the current PrimaryKey.</param>
    /// <returns>true if the specified PrimaryKey is equal to the current PrimaryKey; otherwise, false.</returns>
    public virtual bool Equals(PrimaryKey? other) =>
        other != null
         && Name == other.Name
         && ColumnNames.SequenceEqual(other.ColumnNames);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames);
}
