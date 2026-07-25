using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using NSchema.Model;
using NSchema.Model.Serialization;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// The local name a configuration refers to a plugin by: the label on a <c>PLUGIN</c> statement, referenced
/// by <c>DATABASE</c> and <c>STATE</c>.
/// </summary>
[TypeConverter(typeof(ParsableTypeConverter<PluginLabel>))]
public sealed record PluginLabel : ValueObject<string>, IParsable<PluginLabel>
{
    /// <summary>
    /// Wraps the label as written.
    /// </summary>
    public PluginLabel(string value) : base(value)
    {
    }

    /// <summary>Wraps a label as written.</summary>
    public static PluginLabel Parse(string s, IFormatProvider? provider = null) => new(s);

    /// <summary>Wraps a label as written.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out PluginLabel result)
    {
        result = s is null ? null : new PluginLabel(s);
        return result is not null;
    }

    /// <summary>
    /// Case-insensitive equality over the written text.
    /// </summary>
    public bool Equals(PluginLabel? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>
    /// Wraps the label as written. One-way: a label never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator PluginLabel?(string? value) => value is null ? null : new PluginLabel(value);
}
