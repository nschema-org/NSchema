namespace NSchema.Postgres.Models;

internal sealed record PrimaryKeyRow(
    string TableSchema,
    string TableName,
    string ConstraintName,
    string ColumnName);
