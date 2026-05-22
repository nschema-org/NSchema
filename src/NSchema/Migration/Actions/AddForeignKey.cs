using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record AddForeignKey(
    string SchemaName,
    string TableName,
    ForeignKey ForeignKey
) : MigrationAction
{
    public override bool IsDestructive => false;
}
