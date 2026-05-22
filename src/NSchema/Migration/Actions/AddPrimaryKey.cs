using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record AddPrimaryKey(
    string SchemaName,
    string TableName,
    PrimaryKey PrimaryKey
) : MigrationAction
{
    public override bool IsDestructive => false;
}
