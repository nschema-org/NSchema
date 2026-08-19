using System.Diagnostics;
using System.Text.Json.Serialization;

namespace NSchema.Model.Types;

/// <summary>
/// Represents a type the engine or an extension provides: present by name, with no declaration.
/// </summary>
[DebuggerDisplay("{Name,nq} (native type)")]
public sealed class NativeType : TypeObject, IEquatable<NativeType>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.NativeType;

    /// <summary>
    /// A native type is never NSchema's to manage.
    /// </summary>
    [JsonIgnore]
    public override bool IsImplicit => true;

    /// <inheritdoc/>
    public override NativeType Clone() => new()
    {
        Name = Name,
        ProvidedBy = ProvidedBy,
        Comment = Comment
    };

    /// <summary>
    /// Structural equality over the name alone; a native type has no declaration.
    /// </summary>
    public bool Equals(NativeType? other) => other is not null && Name == other.Name;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NativeType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Name.GetHashCode();
}
