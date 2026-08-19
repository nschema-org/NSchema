using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents granting specific privileges on a table to a role in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Role">The name of the role to which the privileges will be granted.</param>
/// <param name="Privileges">The specific privileges being granted to the role for the table.</param>
public sealed record GrantTablePrivileges(ObjectAddress Table, SqlIdentifier Role, TablePrivilege Privileges) : MigrationAction;
