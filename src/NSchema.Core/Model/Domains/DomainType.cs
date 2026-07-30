using System.Diagnostics;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;

namespace NSchema.Model.Domains;

/// <summary>
/// Represents a database domain: a schema-scoped named type built on a base type. Adopts its checks.
/// </summary>
[DebuggerDisplay("{Name,nq} (domain)")]
public sealed class DomainType : SchemaObject, IEquatable<DomainType>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.Domain;

    /// <summary>
    /// The underlying base type; a change to it is planned as a drop + recreate.
    /// </summary>
    public required SqlType DataType { get; set; }

    /// <summary>
    /// An optional default expression, stored verbatim (opaque SQL); <see langword="null"/> when none.
    /// </summary>
    public SqlDefaultExpression? Default { get; set; }

    /// <summary>
    /// Whether the domain forbids <c>NULL</c>.
    /// </summary>
    public bool NotNull { get; set; }

    /// <summary>
    /// The domain's <c>CHECK</c> constraints (their expressions reference the domain's <c>VALUE</c>); empty when none.
    /// </summary>
    public ObjectMemberCollection<CheckConstraint> Checks
    {
        get => field ??= new ObjectMemberCollection<CheckConstraint>(this);
        init { value.Attach(this); field = value; }
    }

    /// <inheritdoc/>
    public override DomainType Clone() => new()
    {
        Name = Name,
        DataType = DataType,
        Default = Default,
        NotNull = NotNull,
        Checks = [.. Checks.Select(c => c.Clone())],
        Comment = Comment,
    };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(DomainType? other) =>
        other is not null
        && Name == other.Name
        && DataType == other.DataType
        && Equals(Default, other.Default)
        && NotNull == other.NotNull
        && Checks.SequenceEqual(other.Checks);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DomainType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, DataType, Default, NotNull);
}
