using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSchema.Model.Serialization;

/// <summary>
/// Serializes an <see cref="ObjectAddress"/> as <c>{ schema, name }</c>, adding <c>kind</c> only when the
/// address is kind-specific — so a kind-free reference carries no null noise.
/// </summary>
internal sealed class ObjectAddressJsonConverter : JsonConverter<ObjectAddress>
{
    /// <inheritdoc />
    public override ObjectAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        SqlIdentifier? schema = null;
        SqlIdentifier? name = null;
        ObjectKind? kind = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var property = reader.GetString();
            reader.Read();
            switch (property?.ToLowerInvariant())
            {
                case "schema": schema = JsonSerializer.Deserialize<SqlIdentifier>(ref reader, options); break;
                case "name": name = JsonSerializer.Deserialize<SqlIdentifier>(ref reader, options); break;
                case "kind": kind = JsonSerializer.Deserialize<ObjectKind?>(ref reader, options); break;
            }
        }
        return new ObjectAddress(
            schema ?? throw new JsonException("An object address needs a schema."),
            name ?? throw new JsonException("An object address needs a name."),
            kind);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ObjectAddress value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("schema");
        JsonSerializer.Serialize(writer, value.Schema, options);
        writer.WritePropertyName("name");
        JsonSerializer.Serialize(writer, value.Name, options);
        if (value.Kind is { } kind)
        {
            writer.WritePropertyName("kind");
            JsonSerializer.Serialize(writer, kind, options);
        }
        writer.WriteEndObject();
    }
}
