using System.Diagnostics.CodeAnalysis;

namespace NSchema.Model;

/// <summary>
/// A column or domain default expression, carried verbatim.
/// </summary>
public sealed record SqlDefaultExpression : ValueObject<string>
{
    /// <summary>
    /// Wraps the verbatim default expression.
    /// </summary>
    public SqlDefaultExpression(string value) : base(value)
    {
    }

    /// <summary>
    /// Compares default expressions on their cosmetics-normalized text — the neutral rule used when no
    /// dialect-aware equivalence is registered.
    /// </summary>
    public static IEqualityComparer<SqlDefaultExpression> CosmeticComparer { get; } = new CosmeticEquality();

    /// <summary>
    /// Wraps the verbatim default expression. One-way: it never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator SqlDefaultExpression?(string? value) => value is null ? null : new SqlDefaultExpression(value);

    private sealed class CosmeticEquality : IEqualityComparer<SqlDefaultExpression>
    {
        public bool Equals(SqlDefaultExpression? x, SqlDefaultExpression? y) =>
            x is null ? y is null : y is not null
                && string.Equals(SqlCosmetics.Normalize(x.Value), SqlCosmetics.Normalize(y.Value), StringComparison.Ordinal);

        public int GetHashCode(SqlDefaultExpression obj) =>
            SqlCosmetics.Normalize(obj.Value).GetHashCode(StringComparison.Ordinal);
    }
}
