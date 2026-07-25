using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Domain.Tables;

/// <summary>
/// Represents revoking specific privileges on a table from a role in the database schema.
/// </summary>
/// <param name="Table">The address of the table.</param>
/// <param name="Role">The name of the role from which the privileges will be revoked.</param>
/// <param name="Privileges">The specific privileges being revoked from the role for the table.</param>
public sealed record RevokeTablePrivileges(ObjectAddress Table, SqlIdentifier Role, TablePrivilege Privileges) : MigrationAction;
