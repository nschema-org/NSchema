using System.Diagnostics;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a primary key constraint in a database schema.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class PrimaryKey : ObjectMember, IEquatable<PrimaryKey>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.PrimaryKey;

    /// <summary>
    /// A list of column names that are part of the primary key constraint.
    /// </summary>
    public required List<SqlIdentifier> ColumnNames { get; init; }

    /// <summary>
    /// Whether the key's backing index orders the table's rows physically rather than sitting beside them;
    /// <see langword="null"/> means the database default.
    /// </summary>
    public bool? Clustered { get; set; }

    /// <inheritdoc/>
    public override PrimaryKey Clone() => new() { Name = Name, ColumnNames = [.. ColumnNames], Clustered = Clustered, Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(PrimaryKey? other) =>
        other is not null
        && Name == other.Name
        && Clustered == other.Clustered
        && ColumnNames.SequenceEqual(other.ColumnNames);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PrimaryKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames.Count, Clustered);

    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})" +
        (Clustered is { } clustered ? clustered ? " CLUSTERED" : " NONCLUSTERED" : "");
}
