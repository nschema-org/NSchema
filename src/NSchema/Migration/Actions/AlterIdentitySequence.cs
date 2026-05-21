using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record AlterIdentitySequence(
    string SchemaName,
    string TableName,
    string ColumnName,
    IdentityOptions? OldOptions,
    IdentityOptions? NewOptions
) : SchemaAction
{
    public override bool IsDestructive => false;
}
