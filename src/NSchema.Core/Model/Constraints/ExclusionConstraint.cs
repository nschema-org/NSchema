using System.Diagnostics;

namespace NSchema.Model.Constraints;

/// <summary>
/// Represents an exclusion constraint: it guarantees that for any two rows, the given operators do not all return
/// true across the listed elements (e.g. <c>EXCLUDE USING gist (room WITH =, during WITH &amp;&amp;)</c> forbids
/// overlapping bookings of the same room).
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ExclusionConstraint : ObjectMember, IEquatable<ExclusionConstraint>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.ExclusionConstraint;

    /// <summary>
    /// The constrained elements, each a column or expression paired with an operator.
    /// </summary>
    public required List<ExclusionElement> Elements { get; init; }

    /// <summary>
    /// The access method backing the constraint (e.g. <c>gist</c>); <see langword="null"/> means the database default.
    /// </summary>
    public SqlIdentifier? Method { get; set; }

    /// <summary>
    /// An optional predicate restricting the constraint to a subset of rows (a partial constraint).
    /// </summary>
    public SqlText? Predicate { get; set; }

    /// <inheritdoc/>
    public override ExclusionConstraint Clone() => new() { Name = Name, Elements = [.. Elements], Method = Method, Predicate = Predicate, Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(ExclusionConstraint? other) =>
        other is not null
        && Name == other.Name
        && Method == other.Method
        && Equals(Predicate, other.Predicate)
        && Elements.SequenceEqual(other.Elements);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ExclusionConstraint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Method, Predicate, Elements.Count);

    private string DebuggerDisplay =>
        $"{Name}: EXCLUDE" +
        (Method is { } m ? $" USING {m}" : "") +
        $" ({string.Join(", ", Elements.Select(e => $"{e.Expression} WITH {e.Operator}"))})";
}
