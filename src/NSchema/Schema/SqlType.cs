namespace NSchema.Schema;

public abstract record SqlType
{
    public static readonly SqlType Boolean = new BooleanType();
    public static readonly SqlType TinyInt = new TinyIntType();
    public static readonly SqlType SmallInt = new SmallIntType();
    public static readonly SqlType Int = new IntType();
    public static readonly SqlType BigInt = new BigIntType();
    public static readonly SqlType Float = new FloatType();
    public static readonly SqlType Double = new DoubleType();
    public static readonly SqlType Text = new TextType();
    public static readonly SqlType Date = new DateType();
    public static readonly SqlType Time = new TimeType();
    public static readonly SqlType DateTime = new DateTimeType();
    public static readonly SqlType DateTimeOffset = new DateTimeOffsetType();
    public static readonly SqlType Guid = new GuidType();

    public static SqlType Decimal(int precision, int scale) => new DecimalType(precision, scale);
    public static SqlType Char(int length) => new CharType(length);
    public static SqlType NChar(int length) => new NCharType(length);
    public static SqlType VarChar(int? maxLength = null) => new VarCharType(maxLength);
    public static SqlType NVarChar(int? maxLength = null) => new NVarCharType(maxLength);
    public static SqlType Binary(int length) => new BinaryType(length);
    public static SqlType VarBinary(int? maxLength = null) => new VarBinaryType(maxLength);
    public static SqlType Custom(string typeName) => new CustomType(typeName);

    public sealed record BooleanType : SqlType;

    public sealed record TinyIntType : SqlType;

    public sealed record SmallIntType : SqlType;

    public sealed record IntType : SqlType;

    public sealed record BigIntType : SqlType;

    public sealed record FloatType : SqlType;

    public sealed record DoubleType : SqlType;

    public sealed record TextType : SqlType;

    public sealed record DateType : SqlType;

    public sealed record TimeType : SqlType;

    public sealed record DateTimeType : SqlType;

    public sealed record DateTimeOffsetType : SqlType;

    public sealed record GuidType : SqlType;

    public sealed record DecimalType(int Precision, int Scale) : SqlType;

    public sealed record CharType(int Length) : SqlType;

    public sealed record NCharType(int Length) : SqlType;

    public sealed record VarCharType(int? MaxLength) : SqlType;

    public sealed record NVarCharType(int? MaxLength) : SqlType;

    public sealed record BinaryType(int Length) : SqlType;

    public sealed record VarBinaryType(int? MaxLength) : SqlType;

    public sealed record CustomType(string TypeName) : SqlType;
}
