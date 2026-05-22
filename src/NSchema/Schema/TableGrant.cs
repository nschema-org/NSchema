namespace NSchema.Schema;

/// <summary>
/// Represents a grant of specific privileges to a role for a table within the database schema.
/// </summary>
/// <param name="Role">The name of the role to which the privileges are granted.</param>
/// <param name="Privileges">The specific privileges that are granted to the role for the table, encapsulated in a TablePrivilege object.</param>
public record TableGrant(string Role, TablePrivilege Privileges);
