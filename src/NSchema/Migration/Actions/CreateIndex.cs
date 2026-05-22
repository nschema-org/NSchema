using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record CreateIndex(
    string SchemaName,
    string TableName,
    TableIndex Index
) : MigrationAction
{
    public override bool IsDestructive => false;
}
