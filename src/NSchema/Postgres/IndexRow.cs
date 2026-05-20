namespace NSchema.Postgres;

internal sealed record IndexRow(
    string   SchemaName,
    string   TableName,
    string   IndexName,
    bool     IsUnique,
    string[] ColumnNames);
