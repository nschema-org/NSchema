namespace NSchema.Postgres;

internal sealed record ColumnRow(
    string  TableSchema,
    string  TableName,
    string  ColumnName,
    string  DataType,
    string  UdtName,
    int?    MaxLength,
    int?    NumericPrecision,
    int?    NumericScale,
    bool    IsNullable,
    string? DefaultExpression,
    bool    IsIdentity);
