using System.Diagnostics;

namespace NSchema.Model.Constraints;

/// <summary>
/// Represents a check constraint in a database schema.
/// </summary>
[DebuggerDisplay("{Name,nq}: {Expression,nq}")]
public sealed class CheckConstraint : ObjectMember, IEquatable<CheckConstraint>
{
    /// <inheritdoc/>
    public override MemberKind Kind => MemberKind.CheckConstraint;

    /// <summary>
    /// The SQL boolean expression the constraint enforces.
    /// </summary>
    public required SqlText Expression { get; set; }

    /// <summary>
    /// The objects the expression calls.
    /// An unqualified reference resolves against <paramref name="schema"/> (the owning object's).
    /// </summary>
    public IReadOnlyList<ObjectAddress> References(SqlIdentifier schema) =>
        Services.ExpressionDependencyScanner.CallSites(Expression.Value, schema);

    /// <inheritdoc/>
    public override CheckConstraint Clone() => new() { Name = Name, Expression = Expression, Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(CheckConstraint? other) =>
        other is not null
        && Name == other.Name
        && Expression == other.Expression;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CheckConstraint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Expression);
}
