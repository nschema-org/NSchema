using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record CreateTable(string SchemaName, Table Table) : MigrationAction
{
    public override bool IsDestructive => false;
}
