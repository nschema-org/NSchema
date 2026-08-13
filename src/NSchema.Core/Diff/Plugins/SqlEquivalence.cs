using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Sequences;

namespace NSchema.Diff.Plugins;

/// <summary>
/// The provider's comparison-side vocabulary.
/// </summary>
/// <remarks>
/// Provides structural equality for types and other SQL elements, to eliminate provider introspection causing drift on a roundtrip.
/// Prefer under-normalizing, when equality is ambiguous, because over-normalizing would silently swallow a real change.
/// </remarks>
public class SqlEquivalence
{
    /// <summary>
    /// Decides whether two type references mean the same type, e.g. folding the dialect's default schema
    /// or a built-in type alias.
    /// </summary>
    public virtual IEqualityComparer<SqlType> Types => EqualityComparer<SqlType>.Default;

    /// <summary>
    /// Whether the engine validates type names.
    /// When <see langword="false"/> (SQLite), any name is a valid type, so every reference resolves.
    /// </summary>
    public virtual bool ValidatesTypeNames => true;

    /// <summary>
    /// Decides whether two default expressions mean the same default, e.g. folding a literal cast the
    /// database adds when it stores one.
    /// </summary>
    public virtual IEqualityComparer<SqlDefaultExpression> Defaults => SqlDefaultExpression.CosmeticComparer;

    /// <summary>
    /// Folds a sequence's options onto the engine's own defaults, reducing an option declared with the value the
    /// engine would have chosen anyway to the <see langword="null"/> that means the same thing.
    /// </summary>
    public virtual SequenceOptions WithDefaults(SequenceOptions options) => options;

    /// <summary>
    /// Folds an identity column's options onto the engine's own defaults.
    /// </summary>
    public virtual IdentityOptions WithDefaults(IdentityOptions options, SqlType columnType) => options;
}
