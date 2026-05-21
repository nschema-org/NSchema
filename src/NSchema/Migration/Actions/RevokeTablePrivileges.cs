using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record RevokeTablePrivileges(string SchemaName, string TableName, string Role, TablePrivilege Privileges) : SchemaAction
{
    public override bool IsDestructive => true;
}
