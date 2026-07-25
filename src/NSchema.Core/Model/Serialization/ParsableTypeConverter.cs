using System.ComponentModel;
using System.Globalization;

namespace NSchema.Model.Serialization;

/// <summary>
/// Converts a string to any <see cref="IParsable{TSelf}"/> through its own <c>Parse</c>, so one converter serves
/// every parsable domain type rather than a bespoke class each.
/// </summary>
internal sealed class ParsableTypeConverter<T> : TypeConverter where T : IParsable<T>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string text ? T.Parse(text, culture ?? CultureInfo.InvariantCulture) : base.ConvertFrom(context, culture, value);
}
