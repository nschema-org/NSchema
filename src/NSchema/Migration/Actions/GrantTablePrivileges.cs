using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record GrantTablePrivileges(string SchemaName, string TableName, string Role, TablePrivilege Privileges) : SchemaAction
{
    public override bool IsDestructive => false;
}
