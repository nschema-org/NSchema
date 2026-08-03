using System.Diagnostics;

namespace NSchema.Model.Sequences;

/// <summary>
/// Represents a sequence: a named integer generator.
/// </summary>
[DebuggerDisplay("{Name,nq} (sequence)")]
public sealed class Sequence : SchemaObject, IEquatable<Sequence>
{
    /// <inheritdoc/>
    public override SchemaObjectKind Kind => SchemaObjectKind.Sequence;

    /// <summary>
    /// The sequence's options; unset options use the provider's defaults.
    /// </summary>
    public SequenceOptions Options { get; set; } = new();

    /// <inheritdoc/>
    public override Sequence Clone() => new()
    {
        Name = Name,
        Options = Options,
        ProvidedBy = ProvidedBy,
        Comment = Comment
    };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(Sequence? other) =>
        other is not null
        && Name == other.Name
        && Options == other.Options;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Sequence other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Options);
}
