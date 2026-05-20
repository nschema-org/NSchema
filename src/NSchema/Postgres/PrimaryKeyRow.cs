namespace NSchema.Postgres;

internal sealed record PrimaryKeyRow(
    string TableSchema,
    string TableName,
    string ConstraintName,
    string ColumnName);
