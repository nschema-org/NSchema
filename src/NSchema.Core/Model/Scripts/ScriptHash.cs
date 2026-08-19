using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace NSchema.Model.Scripts;

/// <summary>
/// The canonical hash of a script body: SHA-256 of the UTF-8 text, as lowercase hex.
/// </summary>
/// <remarks>
/// Hex is case-insensitive, so the value normalizes to lowercase on wrap and equality stays exact.
/// </remarks>
public sealed record ScriptHash : ValueObject<string>
{
    /// <summary>
    /// Wraps a rendered hash.
    /// </summary>
    public ScriptHash(string value) : base(value)
    {
    }

    /// <summary>
    /// Computes the canonical hash of <paramref name="body"/>.
    /// </summary>
    public static ScriptHash Compute(SqlText body) =>
        new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body.Value))));

    /// <summary>
    /// Wraps a rendered hash. One-way: a hash never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator ScriptHash?(string? value) => value is null ? null : new ScriptHash(value);

    /// <inheritdoc />
    protected override string Normalize(string value) => value.ToLowerInvariant();
}
