namespace NSchema.Postgres.Models;

internal sealed record TableGrantRow(string SchemaName, string TableName, string Role, string Privilege);
