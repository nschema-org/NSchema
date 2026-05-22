namespace NSchema.Schema;

/// <summary>
/// Represents a SQL data type that can be used for defining columns in a database schema.
/// </summary>
public abstract record SqlType
{
    /// <summary>
    /// The SQL boolean type, representing true/false values.
    /// </summary>
    public static readonly SqlType Boolean = new BooleanType();

    /// <summary>
    /// The SQL tinyint type, representing small integer values (0 to 255).
    /// </summary>
    public static readonly SqlType TinyInt = new TinyIntType();

    /// <summary>
    /// The SQL smallint type, representing small integer values (-32,768 to 32,767).
    /// </summary>
    public static readonly SqlType SmallInt = new SmallIntType();

    /// <summary>
    /// The SQL int type, representing integer values (-2,147,483,648 to 2,147,483,647).
    /// </summary>
    public static readonly SqlType Int = new IntType();

    /// <summary>
    /// The SQL bigint type, representing large integer values (-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807).
    /// </summary>
    public static readonly SqlType BigInt = new BigIntType();

    /// <summary>
    /// The SQL float type, representing approximate numeric values with floating decimal points.
    /// </summary>
    public static readonly SqlType Float = new FloatType();

    /// <summary>
    /// The SQL double type, representing approximate numeric values with double precision floating decimal points.
    /// </summary>
    public static readonly SqlType Double = new DoubleType();

    /// <summary>
    /// The SQL text type, representing large character data.
    /// </summary>
    public static readonly SqlType Text = new TextType();

    /// <summary>
    /// The SQL date type, representing calendar dates (year, month, day).
    /// </summary>
    public static readonly SqlType Date = new DateType();

    /// <summary>
    /// The SQL time type, representing time of day (hour, minute, second).
    /// </summary>
    public static readonly SqlType Time = new TimeType();

    /// <summary>
    /// The SQL datetime type, representing date and time values (year, month, day, hour, minute, second).
    /// </summary>
    public static readonly SqlType DateTime = new DateTimeType();

    /// <summary>
    /// The SQL datetimeoffset type, representing date and time values with time zone awareness (year, month, day, hour, minute, second, time zone offset).
    /// </summary>
    public static readonly SqlType DateTimeOffset = new DateTimeOffsetType();

    /// <summary>
    /// The SQL guid type, representing globally unique identifiers (GUIDs) or universally unique identifiers (UUIDs).
    /// </summary>
    public static readonly SqlType Guid = new GuidType();

    /// <summary>
    /// The SQL decimal type, representing fixed-point numeric values with specified precision and scale.
    /// </summary>
    /// <param name="precision">The total number of digits that can be stored, both to the left and right of the decimal point.</param>
    /// <param name="scale">The number of digits that can be stored to the right of the decimal point. The scale must be less than or equal to the precision.</param>
    /// <returns>A new instance of <see cref="DecimalType"/> with the specified precision and scale.</returns>
    public static SqlType Decimal(int precision, int scale) => new DecimalType(precision, scale);

    /// <summary>
    /// The SQL character types, representing fixed-length and variable-length character data.
    /// </summary>
    /// <param name="length">Specifies the number of characters or bytes.</param>
    /// <returns>A new instance of <see cref="CharType"/> with the specified length.</returns>
    public static SqlType Char(int length) => new CharType(length);

    /// <summary>
    /// The SQL nchar type, representing fixed-length Unicode character data.
    /// </summary>
    /// <param name="length">Specifies the number of characters.</param>
    /// <returns>A new instance of <see cref="NCharType"/> with the specified length.</returns>
    public static SqlType NChar(int length) => new NCharType(length);

    /// <summary>
    /// The SQL varchar type, representing variable-length character data.
    /// </summary>
    /// <param name="maxLength">Specifies the maximum number of characters that can be stored. If null, the maximum length is determined by the database system.</param>
    /// <returns>A new instance of <see cref="VarCharType"/> with the specified maximum length.</returns>
    public static SqlType VarChar(int? maxLength = null) => new VarCharType(maxLength);

    /// <summary>
    /// The SQL nvarchar type, representing variable-length Unicode character data.
    /// </summary>
    /// <param name="maxLength">Specifies the maximum number of characters that can be stored. If null, the maximum length is determined by the database system.</param>
    /// <returns>A new instance of <see cref="NVarCharType"/> with the specified maximum length.</returns>
    public static SqlType NVarChar(int? maxLength = null) => new NVarCharType(maxLength);

    /// <summary>
    /// The SQL binary types, representing fixed-length and variable-length binary data.
    /// </summary>
    /// <param name="length">Specifies the number of bytes.</param>
    /// <returns>A new instance of <see cref="BinaryType"/> with the specified length.</returns>
    public static SqlType Binary(int length) => new BinaryType(length);

    /// <summary>
    /// The SQL varbinary type, representing variable-length binary data.
    /// </summary>
    /// <param name="maxLength">Specifies the maximum number of bytes that can be stored. If null, the maximum length is determined by the database system.</param>
    /// <returns>A new instance of <see cref="VarBinaryType"/> with the specified maximum length.</returns>
    public static SqlType VarBinary(int? maxLength = null) => new VarBinaryType(maxLength);

    /// <summary>
    /// Creates a custom SQL type with the specified name. This can be used for database-specific types or user-defined types that are not covered by the predefined types in this class.
    /// </summary>
    /// <param name="typeName">The name of the custom SQL type. This should be a valid SQL type name recognized by the target database system.</param>
    /// <returns>A new instance of <see cref="CustomType"/> with the specified type name.</returns>
    public static SqlType Custom(string typeName) => new CustomType(typeName);

    /// <summary>
    /// The SQL boolean type, representing true/false values.
    /// </summary>
    public sealed record BooleanType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "boolean";
    }

    /// <summary>
    /// The SQL tinyint type, representing small integer values (0 to 255).
    /// </summary>
    public sealed record TinyIntType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "tinyint";
    }

    /// <summary>
    /// The SQL smallint type, representing small integer values (-32,768 to 32,767).
    /// </summary>
    public sealed record SmallIntType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "smallint";
    }

    /// <summary>
    /// The SQL int type, representing integer values (-2,147,483,648 to 2,147,483,647).
    /// </summary>
    public sealed record IntType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "int";
    }

    /// <summary>
    /// The SQL bigint type, representing large integer values (-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807).
    /// </summary>
    public sealed record BigIntType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "bigint";
    }

    /// <summary>
    /// The SQL float type, representing approximate numeric values with floating decimal points.
    /// </summary>
    public sealed record FloatType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "float";
    }

    /// <summary>
    /// The SQL double type, representing approximate numeric values with double precision floating decimal points.
    /// </summary>
    public sealed record DoubleType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "double";
    }

    /// <summary>
    /// The SQL text type, representing large character data.
    /// </summary>
    public sealed record TextType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "text";
    }

    /// <summary>
    /// The SQL date type, representing calendar dates (year, month, day).
    /// </summary>
    public sealed record DateType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "date";
    }

    /// <summary>
    /// The SQL time type, representing time of day (hour, minute, second).
    /// </summary>
    public sealed record TimeType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "time";
    }

    /// <summary>
    /// The SQL datetime type, representing date and time values (year, month, day, hour, minute, second).
    /// </summary>
    public sealed record DateTimeType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "datetime";
    }

    /// <summary>
    /// The SQL datetimeoffset type, representing date and time values with time zone awareness (year, month, day, hour, minute, second, time zone offset).
    /// </summary>
    public sealed record DateTimeOffsetType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "datetimeoffset";
    }

    /// <summary>
    /// The SQL guid type, representing globally unique identifiers (GUIDs) or universally unique identifiers (UUIDs).
    /// </summary>
    public sealed record GuidType : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => "guid";
    }

    /// <summary>
    /// The SQL decimal type, representing fixed-point numeric values with specified precision and scale.
    /// </summary>
    /// <param name="Precision">The total number of digits that can be stored, both to the left and right of the decimal point.</param>
    /// <param name="Scale">The number of digits that can be stored to the right of the decimal point. The scale must be less than or equal to the precision.</param>
    public sealed record DecimalType(int Precision, int Scale) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => $"decimal({Precision},{Scale})";
    }

    /// <summary>
    /// The SQL character types, representing fixed-length character data.
    /// </summary>
    /// <param name="Length">Specifies the number of characters or bytes.</param>
    public sealed record CharType(int Length) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => $"char({Length})";
    }

    /// <summary>
    /// The SQL nchar type, representing fixed-length Unicode character data.
    /// </summary>
    /// <param name="Length">Specifies the number of characters.</param>
    public sealed record NCharType(int Length) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => $"nchar({Length})";
    }

    /// <summary>
    /// The SQL varchar type, representing variable-length character data.
    /// </summary>
    /// <param name="MaxLength">Specifies the maximum number of characters that can be stored. If null, the maximum length is determined by the database system.</param>
    public sealed record VarCharType(int? MaxLength) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => MaxLength is { } n ? $"varchar({n})" : "varchar";
    }

    /// <summary>
    /// The SQL nvarchar type, representing variable-length Unicode character data.
    /// </summary>
    /// <param name="MaxLength">Specifies the maximum number of characters that can be stored. If null, the maximum length is determined by the database system.</param>
    public sealed record NVarCharType(int? MaxLength) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => MaxLength is { } n ? $"nvarchar({n})" : "nvarchar";
    }

    /// <summary>
    /// The SQL binary types, representing fixed-length binary data.
    /// </summary>
    /// <param name="Length">Specifies the number of bytes.</param>
    public sealed record BinaryType(int Length) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => $"binary({Length})";
    }

    /// <summary>
    /// The SQL varbinary type, representing variable-length binary data.
    /// </summary>
    /// <param name="MaxLength">Specifies the maximum number of bytes that can be stored. If null, the maximum length is determined by the database system.</param>
    public sealed record VarBinaryType(int? MaxLength) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => MaxLength is { } n ? $"varbinary({n})" : "varbinary";
    }

    /// <summary>
    /// The SQL custom type, representing a user-defined or database-specific type that is not covered by the predefined types in this class. The type name should be a valid SQL type name recognized by the target database system.
    /// </summary>
    /// <param name="TypeName">The name of the custom SQL type. This should be a valid SQL type name recognized by the target database system.</param>
    public sealed record CustomType(string TypeName) : SqlType
    {
        /// <inheritdoc />
        public override string ToString() => TypeName;
    }
}
