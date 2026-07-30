using System.Diagnostics;

namespace NSchema.Model.Constraints;

/// <summary>
/// Represents a unique constraint in a database schema.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class UniqueConstraint : ObjectMember, IEquatable<UniqueConstraint>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.UniqueConstraint;

    /// <summary>
    /// A list of column names that are part of the unique constraint.
    /// </summary>
    public required List<SqlIdentifier> ColumnNames { get; init; }

    /// <inheritdoc/>
    public override UniqueConstraint Clone() => new() { Name = Name, ColumnNames = [.. ColumnNames], Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(UniqueConstraint? other) =>
        other is not null
        && Name == other.Name
        && ColumnNames.SequenceEqual(other.ColumnNames);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UniqueConstraint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames.Count);

    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})";
}
