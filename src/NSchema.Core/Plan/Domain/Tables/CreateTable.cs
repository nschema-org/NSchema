using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents the creation of a new table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema in which the new table will be created.</param>
/// <param name="Table">The definition of the new table to be created, including its name, columns, and constraints.</param>
public sealed record CreateTable(SqlIdentifier SchemaName, Table Table) : MigrationAction;
