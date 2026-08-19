using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSchema.Model.Serialization;

/// <summary>
/// Serializes any <see cref="IParsable{TSelf}"/> as its canonical text, round-tripping through its own <c>Parse</c>.
/// </summary>
public sealed class ParsableJsonConverter<T> : JsonConverter<T> where T : IParsable<T>
{
    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } text ? T.Parse(text, CultureInfo.InvariantCulture) : default;

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
