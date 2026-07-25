using System.Diagnostics.CodeAnalysis;

namespace NSchema.Model;

/// <summary>
/// A SQL object name.
/// </summary>
public sealed record SqlIdentifier : ValueObject<string>, IComparable<SqlIdentifier>
{
    /// <summary>
    /// Wraps the name as written.
    /// </summary>
    public SqlIdentifier(string value) : base(value)
    {
    }

    /// <summary>
    /// Case-sensitive (ordinal) equality.
    /// </summary>
    public bool Equals(SqlIdentifier? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public int CompareTo(SqlIdentifier? other) => StringComparer.Ordinal.Compare(Value, other?.Value);

    /// <summary>
    /// Wraps the name as written. One-way: a name never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator SqlIdentifier?(string? value) => value is null ? null : new SqlIdentifier(value);
}
