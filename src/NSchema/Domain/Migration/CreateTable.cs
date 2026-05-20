using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration;

public sealed record CreateTable(string SchemaName, Table Table) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
