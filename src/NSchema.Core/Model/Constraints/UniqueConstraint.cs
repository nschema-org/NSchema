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

    /// <summary>
    /// Whether the constraint's backing index orders the table's rows physically rather than sitting beside
    /// them; <see langword="null"/> means the database default.
    /// </summary>
    public bool? Clustered { get; set; }

    /// <inheritdoc/>
    public override UniqueConstraint Clone() => new() { Name = Name, ColumnNames = [.. ColumnNames], Clustered = Clustered, Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(UniqueConstraint? other) =>
        other is not null
        && Name == other.Name
        && Clustered == other.Clustered
        && ColumnNames.SequenceEqual(other.ColumnNames);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is UniqueConstraint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames.Count, Clustered);

    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})" +
        (Clustered is { } clustered ? clustered ? " CLUSTERED" : " NONCLUSTERED" : "");
}
