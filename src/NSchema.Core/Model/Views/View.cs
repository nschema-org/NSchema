using System.Diagnostics;
using NSchema.Model.Indexes;

namespace NSchema.Model.Views;

/// <summary>
/// Represents a database view: a named query stored in a schema. Adopts its indexes.
/// </summary>
[DebuggerDisplay("{Name,nq} (view)")]
public sealed class View : SchemaObject, IEquatable<View>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.View;

    /// <summary>
    /// The view's defining query, stored verbatim (the text after <c>AS</c>).
    /// </summary>
    public required SqlText Body { get; set; }

    /// <summary>
    /// The objects the view reads, derived from <see cref="Body"/>.
    /// </summary>
    public List<ObjectAddress> DependsOn { get; init; } = [];

    /// <summary>
    /// Whether this is a materialized view (stores its result set).
    /// </summary>
    public bool IsMaterialized { get; set; }

    /// <summary>
    /// Indexes on the view (materialized views only; empty for a plain view).
    /// </summary>
    public ObjectMemberCollection<TableIndex> Indexes
    {
        get => field ??= new ObjectMemberCollection<TableIndex>(this);
        init { value.Attach(this); field = value; }
    }

    /// <inheritdoc/>
    public override View Clone() => new()
    {
        Name = Name,
        Body = Body,
        DependsOn = [.. DependsOn],
        IsMaterialized = IsMaterialized,
        Indexes = [.. Indexes.Select(i => i.Clone())],
        ProvidedBy = ProvidedBy,
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(View? other) =>
        other is not null
        && Name == other.Name
        && Body == other.Body
        && IsMaterialized == other.IsMaterialized
        && DependsOn.SequenceEqual(other.DependsOn)
        && Indexes.SequenceEqual(other.Indexes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is View other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Body, IsMaterialized);
}
