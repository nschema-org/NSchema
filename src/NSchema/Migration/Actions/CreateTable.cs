using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record CreateTable(string SchemaName, Table Table) : SchemaAction
{
    public override bool IsDestructive => false;
}
