using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record CreateTable(string SchemaName, Table Table) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
