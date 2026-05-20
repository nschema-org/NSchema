namespace NSchema.Postgres.Models;

internal sealed record ForeignKeyRow(
    string TableSchema,
    string TableName,
    string ConstraintName,
    string[] ColumnNames,
    string ForeignSchema,
    string ForeignTable,
    string[] ForeignColumnNames,
    char UpdateRule,
    char DeleteRule);
