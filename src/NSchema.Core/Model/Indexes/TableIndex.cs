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
    /// Whether the index orders the relation's rows physically rather than sitting beside them;
    /// <see langword="null"/> means the database default.
    /// </summary>
    public bool? Clustered { get; set; }

    /// <summary>
    /// Non-key columns carried in the index leaf pages (a covering <c>INCLUDE</c> clause).
    /// </summary>
    public List<SqlIdentifier> Include { get; init; } = [];

    /// <summary>
    /// The XML facet, when this indexes the shredded contents of an XML column rather than a value;
    /// <see langword="null"/> for an ordinary index.
    /// </summary>
    public XmlIndexDefinition? Xml { get; set; }

    /// <inheritdoc/>
    public override TableIndex Clone() => new()
    {
        Name = Name,
        Columns = [.. Columns],
        IsUnique = IsUnique,
        Predicate = Predicate,
        Method = Method,
        Clustered = Clustered,
        Include = [.. Include],
        Xml = Xml,
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
        && Clustered == other.Clustered
        && Equals(Predicate, other.Predicate)
        && Equals(Xml, other.Xml)
        && Columns.SequenceEqual(other.Columns)
        && Include.SequenceEqual(other.Include);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TableIndex other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, IsUnique, Method, Predicate, Columns.Count, Include.Count, Xml, Clustered);

    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", Columns.Select(c => c.Expression))})" +
        (Xml is { } xml ? $" XML {xml.Kind}" : "") +
        (IsUnique ? " UNIQUE" : "") +
        (Clustered is { } clustered ? clustered ? " CLUSTERED" : " NONCLUSTERED" : "") +
        (Method is { } m ? $" USING {m}" : "") +
        (Predicate is { } p ? $" WHERE {p}" : "");
}
