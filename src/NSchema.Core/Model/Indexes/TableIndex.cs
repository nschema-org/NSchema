using System.Diagnostics;

namespace NSchema.Model.Indexes;

/// <summary>
/// Represents an index on a table or materialized view within the database schema.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class TableIndex : ObjectMember, IEquatable<TableIndex>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.Index;

    /// <summary>
    /// The index keys (columns or expressions) in order.
    /// </summary>
    public required List<IndexColumn> Columns { get; init; }

    /// <summary>
    /// A boolean value indicating whether the index enforces uniqueness on the indexed columns.
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// An optional predicate that defines a partial index.
    /// </summary>
    public SqlText? Predicate { get; set; }

    /// <summary>
    /// The access method; <see langword="null"/> means the database default (B-tree).
    /// </summary>
    public SqlIdentifier? Method { get; set; }

    /// <summary>
    /// Non-key columns carried in the index leaf pages (a covering <c>INCLUDE</c> clause).
    /// </summary>
    public List<SqlIdentifier> Include { get; init; } = [];

    /// <inheritdoc/>
    public override TableIndex Clone() => new()
    {
        Name = Name,
        Columns = [.. Columns],
        IsUnique = IsUnique,
        Predicate = Predicate,
        Method = Method,
        Include = [.. Include],
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the parent and the comment are excluded.
    /// </summary>
    public bool Equals(TableIndex? other) =>
        other is not null
        && Name == other.Name
        && IsUnique == other.IsUnique
        && Method == other.Method
        && Equals(Predicate, other.Predicate)
        && Columns.SequenceEqual(other.Columns)
        && Include.SequenceEqual(other.Include);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TableIndex other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, IsUnique, Method, Predicate, Columns.Count, Include.Count);

    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", Columns.Select(c => c.Expression))})" +
        (IsUnique ? " UNIQUE" : "") +
        (Method is { } m ? $" USING {m}" : "") +
        (Predicate is { } p ? $" WHERE {p}" : "");
}
