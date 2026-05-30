using System.Text.Json;
using System.Text.Json.Serialization;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// Serializes the polymorphic <see cref="SqlType"/> hierarchy as a tagged JSON object.
/// </summary>
/// <remarks>
/// This lives in the stateto keep serialization concerns out of the domain model.
/// </remarks>
internal sealed class SqlTypeJsonConverter : JsonConverter<SqlType>
{
    public override SqlType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected an object for {nameof(SqlType)} but found {reader.TokenType}.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty("kind", out var kindElement) || kindElement.GetString() is not { } kind)
        {
            throw new JsonException($"A {nameof(SqlType)} object must contain a non-null \"kind\" property.");
        }

        int Int(string name) => root.TryGetProperty(name, out var element)
            ? element.GetInt32()
            : throw new JsonException($"SqlType \"{kind}\" requires an integer \"{name}\" property.");

        int? OptionalInt(string name) => root.TryGetProperty(name, out var element) && element.ValueKind is not JsonValueKind.Null
            ? element.GetInt32()
            : null;

        string String(string name) => root.TryGetProperty(name, out var element) && element.GetString() is { } value
            ? value
            : throw new JsonException($"SqlType \"{kind}\" requires a string \"{name}\" property.");

        return kind switch
        {
            "boolean" => SqlType.Boolean,
            "tinyint" => SqlType.TinyInt,
            "smallint" => SqlType.SmallInt,
            "int" => SqlType.Int,
            "bigint" => SqlType.BigInt,
            "float" => SqlType.Float,
            "double" => SqlType.Double,
            "text" => SqlType.Text,
            "date" => SqlType.Date,
            "time" => SqlType.Time,
            "datetime" => SqlType.DateTime,
            "datetimeoffset" => SqlType.DateTimeOffset,
            "guid" => SqlType.Guid,
            "decimal" => SqlType.Decimal(Int("precision"), Int("scale")),
            "char" => SqlType.Char(Int("length")),
            "nchar" => SqlType.NChar(Int("length")),
            "binary" => SqlType.Binary(Int("length")),
            "varchar" => SqlType.VarChar(OptionalInt("maxLength")),
            "nvarchar" => SqlType.NVarChar(OptionalInt("maxLength")),
            "varbinary" => SqlType.VarBinary(OptionalInt("maxLength")),
            "custom" => SqlType.Custom(String("typeName")),
            _ => throw new JsonException($"Unknown {nameof(SqlType)} kind \"{kind}\"."),
        };
    }

    public override void Write(Utf8JsonWriter writer, SqlType value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case SqlType.BooleanType: writer.WriteString("kind", "boolean"); break;
            case SqlType.TinyIntType: writer.WriteString("kind", "tinyint"); break;
            case SqlType.SmallIntType: writer.WriteString("kind", "smallint"); break;
            case SqlType.IntType: writer.WriteString("kind", "int"); break;
            case SqlType.BigIntType: writer.WriteString("kind", "bigint"); break;
            case SqlType.FloatType: writer.WriteString("kind", "float"); break;
            case SqlType.DoubleType: writer.WriteString("kind", "double"); break;
            case SqlType.TextType: writer.WriteString("kind", "text"); break;
            case SqlType.DateType: writer.WriteString("kind", "date"); break;
            case SqlType.TimeType: writer.WriteString("kind", "time"); break;
            case SqlType.DateTimeType: writer.WriteString("kind", "datetime"); break;
            case SqlType.DateTimeOffsetType: writer.WriteString("kind", "datetimeoffset"); break;
            case SqlType.GuidType: writer.WriteString("kind", "guid"); break;
            case SqlType.DecimalType decimalType:
                writer.WriteString("kind", "decimal");
                writer.WriteNumber("precision", decimalType.Precision);
                writer.WriteNumber("scale", decimalType.Scale);
                break;
            case SqlType.CharType charType:
                writer.WriteString("kind", "char");
                writer.WriteNumber("length", charType.Length);
                break;
            case SqlType.NCharType ncharType:
                writer.WriteString("kind", "nchar");
                writer.WriteNumber("length", ncharType.Length);
                break;
            case SqlType.BinaryType binaryType:
                writer.WriteString("kind", "binary");
                writer.WriteNumber("length", binaryType.Length);
                break;
            case SqlType.VarCharType varCharType:
                writer.WriteString("kind", "varchar");
                WriteOptionalLength(writer, varCharType.MaxLength);
                break;
            case SqlType.NVarCharType nvarCharType:
                writer.WriteString("kind", "nvarchar");
                WriteOptionalLength(writer, nvarCharType.MaxLength);
                break;
            case SqlType.VarBinaryType varBinaryType:
                writer.WriteString("kind", "varbinary");
                WriteOptionalLength(writer, varBinaryType.MaxLength);
                break;
            case SqlType.CustomType customType:
                writer.WriteString("kind", "custom");
                writer.WriteString("typeName", customType.TypeName);
                break;
            default:
                throw new JsonException($"Unsupported {nameof(SqlType)} \"{value.GetType().Name}\".");
        }
        writer.WriteEndObject();
    }

    private static void WriteOptionalLength(Utf8JsonWriter writer, int? maxLength)
    {
        if (maxLength is { } length)
        {
            writer.WriteNumber("maxLength", length);
        }
    }
}
