using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record AddColumn(string SchemaName, string TableName, Column Column) : MigrationAction
{
    public override bool IsDestructive => false;
}
