using System.Diagnostics;

namespace NSchema.Model.Enums;

/// <summary>
/// Represents an enum type: a named, ordered set of string values.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Values.Count} values)")]
public sealed class EnumType : SchemaObject, IEquatable<EnumType>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.Enum;

    /// <summary>
    /// The allowed values, in order.
    /// </summary>
    public List<EnumLabel> Values { get; init; } = [];

    /// <inheritdoc/>
    public override EnumType Clone() => new() { Name = Name, Values = [.. Values], Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(EnumType? other) =>
        other is not null
        && Name == other.Name
        && Values.SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EnumType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Values.Count);
}
