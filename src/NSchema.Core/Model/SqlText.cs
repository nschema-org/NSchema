using System.Diagnostics.CodeAnalysis;

namespace NSchema.Model;

/// <summary>
/// A verbatim fragment of SQL that NSchema carries but does not interpret.
/// </summary>
public sealed record SqlText : ValueObject<string>
{
    /// <summary>
    /// Wraps the verbatim SQL text.
    /// </summary>
    public SqlText(string value) : base(value)
    {
    }

    /// <summary>
    /// Whether this text and <paramref name="other"/> differ only cosmetically, so the differences a database
    /// introduces when it stores and re-emits a definition do not read as a change.
    /// </summary>
    /// <remarks>
    /// This handles only <strong>cosmetic</strong> differences — insignificant whitespace and a trailing
    /// statement terminator — and does so <em>literal-aware</em>: text inside single-quoted strings and
    /// double-quoted identifiers is preserved verbatim, so <c>'a  b'</c> is never conflated with <c>'a b'</c>.
    /// Under-normalizing leaves a harmless phantom difference; over-normalizing would silently swallow a real
    /// change, so the bias is deliberately conservative.
    /// <para>
    /// It intentionally does <strong>not</strong> normalize keyword casing or the semantic rewrites a database
    /// performs (name qualification, <c>*</c> expansion, injected casts). Those require the database itself to
    /// canonicalize the definition and are reconciled provider-side (by storing/importing the DB-reported form).
    /// </para>
    /// </remarks>
    public bool EquivalentTo(SqlText other) =>
        string.Equals(SqlCosmetics.Normalize(Value), SqlCosmetics.Normalize(other.Value), StringComparison.Ordinal);

    /// <summary>
    /// Wraps the verbatim SQL text. One-way: text never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator SqlText?(string? value) => value is null ? null : new SqlText(value);
}
