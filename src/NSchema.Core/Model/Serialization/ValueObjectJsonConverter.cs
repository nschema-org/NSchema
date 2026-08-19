using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSchema.Model.Serialization;

/// <summary>
/// Serializes a <see cref="ValueObject{TValue}"/> as its bare value.
/// </summary>
internal sealed class ValueObjectJsonConverter : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => GetValueType(typeToConvert) is not null;

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = GetValueType(typeToConvert)
            ?? throw new ArgumentException($"'{typeToConvert}' is not a value object.", nameof(typeToConvert));

        return (JsonConverter)Activator.CreateInstance(typeof(Converter<,>).MakeGenericType(typeToConvert, valueType))!;
    }

    private static Type? GetValueType(Type type)
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
        private static readonly Func<TValue, T> _create = BuildFactory();

        private static Func<TValue, T> BuildFactory()
        {
            var ctor = typeof(T).GetConstructor([typeof(TValue)])
                ?? throw new InvalidOperationException(
                    $"{typeof(T).Name} must expose a public ({typeof(TValue).Name}) constructor to serialize as a value object.");
            var value = Expression.Parameter(typeof(TValue), "value");
            return Expression.Lambda<Func<TValue, T>>(Expression.New(ctor, value), value).Compile();
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            _create(JsonSerializer.Deserialize<TValue>(ref reader, options)
                ?? throw new JsonException($"A {typeof(T).Name} must be a {typeof(TValue).Name}."));

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Value, options);

        public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            typeof(TValue) == typeof(string)
                ? _create((TValue)(object)(reader.GetString() ?? throw new JsonException($"A {typeof(T).Name} must be a string.")))
                : throw new NotSupportedException($"{typeof(T).Name} is not string-backed, so it cannot key an object.");

        public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            writer.WritePropertyName(value.Value as string
                ?? throw new NotSupportedException($"{typeof(T).Name} is not string-backed, so it cannot key an object."));
    }
}
