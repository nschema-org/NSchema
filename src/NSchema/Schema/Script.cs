namespace NSchema.Schema;

/// <summary>
/// Represents a SQL script that can be executed as part of the database schema.
/// </summary>
/// <param name="Name">The name of the script, which can be used for identification and reference purposes.</param>
/// <param name="Sql">The actual SQL code contained in the script, which defines the operations to be performed on the database when the script is executed.</param>
public record Script(string Name, string Sql);
