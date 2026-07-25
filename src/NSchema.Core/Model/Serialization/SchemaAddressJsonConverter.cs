using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSchema.Model.Serialization;

/// <summary>
/// Serializes a <see cref="SchemaAddress"/> as its bare schema name, matching how a <see cref="SqlIdentifier"/> renders.
/// </summary>
internal sealed class SchemaAddressJsonConverter : JsonConverter<SchemaAddress>
{
    /// <inheritdoc />
    public override SchemaAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? throw new JsonException("A schema address must be a string."));

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SchemaAddress value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Schema.Value);
}
