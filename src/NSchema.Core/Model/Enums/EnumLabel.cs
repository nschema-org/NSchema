using System.Diagnostics.CodeAnalysis;

namespace NSchema.Model.Enums;

/// <summary>
/// A single value of an enum type: a data label whose identity is its exact written text.
/// </summary>
public sealed record EnumLabel : ValueObject<string>
{
    /// <summary>
    /// Wraps the label as written.
    /// </summary>
    public EnumLabel(string value) : base(value)
    {
    }

    /// <summary>
    /// Case-sensitive (ordinal) equality.
    /// </summary>
    public bool Equals(EnumLabel? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>
    /// Wraps the label as written. One-way: a label never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator EnumLabel?(string? value) => value is null ? null : new EnumLabel(value);
}
