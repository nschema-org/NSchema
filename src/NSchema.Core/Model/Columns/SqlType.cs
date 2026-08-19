namespace NSchema.Model.Columns;

/// <summary>
/// Represents a SQL data type used for defining columns in a database schema.
/// </summary>
/// <param name="Name">The type name, e.g. <c>"varchar"</c>, <c>"decimal"</c>, <c>"citext"</c>. Written casing is
/// preserved; names compare case-insensitively, as identifiers do.</param>
public sealed record SqlType(SqlIdentifier Name)
{
    /// <summary>
    /// The type name.
    /// </summary>
    public SqlIdentifier Name { get; init; } = Name ?? throw new ArgumentNullException(nameof(Name));

    /// <summary>
    /// The schema qualifying a user-defined type.
    /// </summary>
    public SqlIdentifier? Schema { get; init; }

    /// <summary>
    /// The length facet for fixed- and variable-length types (char/varchar/binary). Null means unbounded.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// The precision facet for <c>decimal</c> (total number of digits).
    /// </summary>
    public int? Precision { get; init; }

    /// <summary>
    /// The scale facet for <c>decimal</c> (digits to the right of the decimal point).
    /// </summary>
    public int? Scale { get; init; }

    /// <summary>
    /// The SQL boolean type, representing true/false values.
    /// </summary>
    public static readonly SqlType Boolean = new("boolean");

    /// <summary>
    /// The SQL tinyint type, representing small integer values (0 to 255).
    /// </summary>
    public static readonly SqlType TinyInt = new("tinyint");

    /// <summary>
    /// The SQL smallint type, representing small integer values (-32,768 to 32,767).
    /// </summary>
    public static readonly SqlType SmallInt = new("smallint");

    /// <summary>
    /// The SQL int type, representing integer values (-2,147,483,648 to 2,147,483,647).
    /// </summary>
    public static readonly SqlType Int = new("int");

    /// <summary>
    /// The SQL bigint type, representing large integer values.
    /// </summary>
    public static readonly SqlType BigInt = new("bigint");

    /// <summary>
    /// The SQL float type, representing approximate single-precision numeric values.
    /// </summary>
    public static readonly SqlType Float = new("float");

    /// <summary>
    /// The SQL double type, representing approximate double-precision numeric values.
    /// </summary>
    public static readonly SqlType Double = new("double");

    /// <summary>
    /// The SQL text type, representing large character data.
    /// </summary>
    public static readonly SqlType Text = new("text");

    /// <summary>
    /// The SQL date type, representing calendar dates (year, month, day).
    /// </summary>
    public static readonly SqlType Date = new("date");

    /// <summary>
    /// The SQL time type, representing time of day (hour, minute, second).
    /// </summary>
    public static readonly SqlType Time = new("time");

    /// <summary>
    /// The SQL datetime type, representing date and time values without time zone.
    /// </summary>
    public static readonly SqlType DateTime = new("datetime");

    /// <summary>
    /// The SQL datetimeoffset type, representing date and time values with time zone awareness.
    /// </summary>
    public static readonly SqlType DateTimeOffset = new("datetimeoffset");

    /// <summary>
    /// The SQL guid type, representing globally unique identifiers (GUIDs/UUIDs).
    /// </summary>
    public static readonly SqlType Guid = new("guid");

    /// <summary>
    /// The SQL decimal type, representing fixed-point numeric values with specified precision and scale.
    /// </summary>
    /// <param name="precision">The total number of digits that can be stored.</param>
    /// <param name="scale">The number of digits that can be stored to the right of the decimal point.</param>
    public static SqlType Decimal(int precision, int scale) => new("decimal") { Precision = precision, Scale = scale };

    /// <summary>
    /// The SQL char type, representing fixed-length character data.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    public static SqlType Char(int length) => new("char") { Length = length };

    /// <summary>
    /// The SQL nchar type, representing fixed-length Unicode character data.
    /// </summary>
    /// <param name="length">The number of characters.</param>
    public static SqlType NChar(int length) => new("nchar") { Length = length };

    /// <summary>
    /// The SQL varchar type, representing variable-length character data.
    /// </summary>
    /// <param name="maxLength">The maximum number of characters, or null for unbounded.</param>
    public static SqlType VarChar(int? maxLength = null) => new("varchar") { Length = maxLength };

    /// <summary>
    /// The SQL nvarchar type, representing variable-length Unicode character data.
    /// </summary>
    /// <param name="maxLength">The maximum number of characters, or null for unbounded.</param>
    public static SqlType NVarChar(int? maxLength = null) => new("nvarchar") { Length = maxLength };

    /// <summary>
    /// The SQL binary type, representing fixed-length binary data.
    /// </summary>
    /// <param name="length">The number of bytes.</param>
    public static SqlType Binary(int length) => new("binary") { Length = length };

    /// <summary>
    /// The SQL varbinary type, representing variable-length binary data.
    /// </summary>
    /// <param name="maxLength">The maximum number of bytes, or null for unbounded.</param>
    public static SqlType VarBinary(int? maxLength = null) => new("varbinary") { Length = maxLength };

    /// <summary>
    /// A SQL type identified by a raw name the library has no special knowledge of, such as a
    /// database-specific or user-defined type (e.g. Postgres' <c>citext</c>).
    /// </summary>
    /// <param name="typeName">The type name recognized by the target database system.</param>
    public static SqlType Custom(string typeName) => new(typeName.Trim());

    /// <summary>
    /// A user-defined type qualified by the schema that owns it (e.g. <c>app.order_status</c>).
    /// </summary>
    /// <param name="schema">The schema the type belongs to.</param>
    /// <param name="typeName">The type's name within that schema.</param>
    public static SqlType Custom(SqlIdentifier schema, string typeName) => new(typeName.Trim()) { Schema = schema };

    /// <summary>
    /// The XML schema collection an <c>xml</c> value is validated against; <see langword="null"/> for an
    /// untyped one, and for every other type.
    /// </summary>
    public XmlTypeBinding? Xml { get; init; }

    /// <summary>
    /// Assesses whether stored values can fail when converted to <paramref name="target"/>.
    /// </summary>
    public TypeConversionRisk ConversionRiskTo(SqlType target)
    {
        var from = Family;
        var to = target.Family;
        if (from == TypeFamily.Unknown || to == TypeFamily.Unknown)
        {
            return TypeConversionRisk.Unknown;
        }

        return (from, to) switch
        {
            (TypeFamily.String, TypeFamily.String) => Capacity > target.Capacity ? TypeConversionRisk.MayFail : TypeConversionRisk.Safe,
            (TypeFamily.Binary, TypeFamily.Binary) => Capacity > target.Capacity ? TypeConversionRisk.MayFail : TypeConversionRisk.Safe,
            (TypeFamily.String, _) => TypeConversionRisk.MayFail,
            (TypeFamily.Integer, TypeFamily.Integer) => target.IntegerRank < IntegerRank ? TypeConversionRisk.MayFail : TypeConversionRisk.Safe,
            (TypeFamily.Integer, TypeFamily.Decimal) => IntegerDigits > target.WholeDigits ? TypeConversionRisk.MayFail : TypeConversionRisk.Safe,
            (TypeFamily.Decimal, TypeFamily.Decimal) => target.WholeDigits < WholeDigits ? TypeConversionRisk.MayFail : TypeConversionRisk.Safe,
            (TypeFamily.Decimal, TypeFamily.Integer) => TypeConversionRisk.MayFail,
            (TypeFamily.Float, TypeFamily.Integer) => TypeConversionRisk.MayFail,
            (TypeFamily.Float, TypeFamily.Decimal) => TypeConversionRisk.MayFail,
            (TypeFamily.Float, TypeFamily.Float) when NameOf(this) == "double" && NameOf(target) == "float" => TypeConversionRisk.MayFail,
            _ => TypeConversionRisk.Safe,
        };
    }

    /// <summary>
    /// Renders the canonical string form, e.g. <c>"bigint"</c>, <c>"varchar(255)"</c>, <c>"app.order_status"</c>.
    /// </summary>
    public override string ToString()
    {
        var qualified = Schema != null ? $"{Schema}.{Name}" : Name.Value;

        if (Xml is { } xml)
        {
            return $"{qualified}({(xml.IsDocument ? "DOCUMENT" : "CONTENT")} {xml.Collection})";
        }

        if (Precision is { } precision)
        {
            return $"{qualified}({precision},{Scale})";
        }

        if (Length is { } length)
        {
            return $"{qualified}({length})";
        }

        return qualified;
    }

    /// <summary>
    /// Parses a built-in SQL type from its canonical string form, normalizing dialect spellings (e.g.
    /// <c>int4</c> to <c>int</c>). An unrecognized name is preserved verbatim as a <see cref="Custom(string)"/> type.
    /// </summary>
    /// <remarks>
    /// The input is an unqualified type expression — a schema qualifier is a structural component
    /// (<see cref="Schema"/>), set via <see cref="Custom(SqlIdentifier, string)"/> when projecting a parsed
    /// type, not recovered by splitting a string here.
    /// </remarks>
    public static SqlType Parse(string value)
    {
        value = value.Trim();
        var (name, args) = Tokenize(value);
        return name switch
        {
            "boolean" or "bool" => Boolean,
            "tinyint" => TinyInt,
            "smallint" or "int2" => SmallInt,
            "int" or "integer" or "int4" => Int,
            "bigint" or "int8" => BigInt,
            "float" or "real" or "float4" => Float,
            "double" or "float8" => Double,
            "text" => Text,
            "date" => Date,
            "time" => Time,
            "datetime" or "timestamp" => DateTime,
            "datetimeoffset" or "timestamptz" => DateTimeOffset,
            "guid" or "uuid" => Guid,
            "decimal" or "numeric" => Decimal(Arg(0), Arg(1)),
            "char" or "character" => Char(Arg(0)),
            "nchar" => NChar(Arg(0)),
            "binary" => Binary(Arg(0)),
            "varchar" => VarChar(OptionalArg(0)),
            "nvarchar" => NVarChar(OptionalArg(0)),
            "varbinary" or "bytea" => VarBinary(OptionalArg(0)),
            _ => Custom(value),
        };

        int Arg(int index) => OptionalArg(index) ?? throw new FormatException($"SqlType \"{name}\" is missing required argument {index} in \"{value}\".");
        int? OptionalArg(int index) => index < args.Count ? int.Parse(args[index]) : null;
    }

    /// <summary>
    /// Splits a type string into its lower-cased base name and any comma-separated arguments,
    /// e.g. <c>"decimal(10, 2)"</c> becomes <c>("decimal", ["10", "2"])</c>.
    /// </summary>
    private static (string Name, IReadOnlyList<string> Args) Tokenize(string value)
    {
        var parenStart = value.IndexOf('(');
        if (parenStart < 0)
        {
            return (value.ToLowerInvariant(), []);
        }

        var parenEnd = value.LastIndexOf(')');
        if (parenEnd < parenStart)
        {
            throw new FormatException($"Malformed SqlType string: \"{value}\".");
        }

        var name = value[..parenStart].ToLowerInvariant();
        var args = value[(parenStart + 1)..parenEnd].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return (name, args);
    }

    private TypeFamily Family => NameOf(this) switch
    {
        "char" or "nchar" or "varchar" or "nvarchar" or "text" => TypeFamily.String,
        "binary" or "varbinary" => TypeFamily.Binary,
        "tinyint" or "smallint" or "int" or "bigint" => TypeFamily.Integer,
        "decimal" => TypeFamily.Decimal,
        "float" or "double" => TypeFamily.Float,
        "boolean" or "date" or "time" or "datetime" or "datetimeoffset" or "guid" => TypeFamily.Other,
        _ => TypeFamily.Unknown,
    };

    private int Capacity => Length ?? int.MaxValue;

    private int IntegerRank => NameOf(this) switch
    {
        "tinyint" => 0,
        "smallint" => 1,
        "int" => 2,
        _ => 3,
    };

    private int IntegerDigits => NameOf(this) switch
    {
        "tinyint" => 3,
        "smallint" => 5,
        "int" => 10,
        _ => 19,
    };

    private int WholeDigits => Precision is { } precision ? precision - (Scale ?? 0) : int.MaxValue;

    private static string NameOf(SqlType type) => type.Name.Value.ToLowerInvariant();

    private enum TypeFamily { String, Binary, Integer, Decimal, Float, Other, Unknown }
}
