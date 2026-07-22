using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Configuration.Model;
using NSchema.Model;

namespace NSchema.Services.Reporting;

/// <summary>
/// The shared NDJSON serialization used by both the JSON messenger and presenter, so the two write identical output
/// without sharing a base class.
/// </summary>
internal static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // SQL bodies contain quotes and angle brackets; relaxed escaping keeps them readable (\" not ") — this is
        // CLI output, not HTML, so the extra-cautious default encoder isn't needed.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new ValueObjectConverter(), new SemanticVersionConverter() },
    };

    // SemanticVersion is a structured record rather than a ValueObject<T>, so serialize it as its canonical text.
    private sealed class SemanticVersionConverter : JsonConverter<SemanticVersion>
    {
        public override SemanticVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("CLI output is write-only.");

        public override void Write(Utf8JsonWriter writer, SemanticVersion value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    public static void Write(TextWriter writer, object @event) => writer.WriteLine(JsonSerializer.Serialize(@event, Options));

    // Value objects (SqlIdentifier, SqlText, ...) serialize as their underlying value, so names and SQL render as
    // plain JSON strings rather than { "value": ... } wrappers.
    private sealed class ValueObjectConverter : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => ValueType(typeToConvert) is not null;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(Converter<,>).MakeGenericType(typeToConvert, ValueType(typeToConvert)!))!;

        private static Type? ValueType(Type type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ValueObject<>))
                {
                    return current.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private sealed class Converter<T, TValue> : JsonConverter<T> where T : ValueObject<TValue>
        {
            public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                throw new NotSupportedException("CLI output is write-only.");

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
                JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
