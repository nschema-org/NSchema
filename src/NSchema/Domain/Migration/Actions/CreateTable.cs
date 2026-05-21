using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Actions;

public sealed record CreateTable(string SchemaName, Table Table) : SchemaAction
{
    public override bool IsDestructive => false;
}
