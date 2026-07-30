using System.Diagnostics;

namespace NSchema.Model.CompositeTypes;

/// <summary>
/// Represents a database composite type: a schema-scoped named tuple of typed <see cref="Fields"/>.
/// </summary>
[DebuggerDisplay("{Name,nq} (composite type, {Fields.Count} fields)")]
public sealed class CompositeType : SchemaObject, IEquatable<CompositeType>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.CompositeType;

    /// <summary>
    /// The fields (attributes) of the type, matched by name; may be empty.
    /// </summary>
    public List<CompositeField> Fields { get; init; } = [];

    /// <inheritdoc/>
    public override CompositeType Clone() => new() { Name = Name, Fields = [.. Fields], Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(CompositeType? other) =>
        other is not null
        && Name == other.Name
        && Fields.SequenceEqual(other.Fields);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CompositeType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Fields.Count);
}
