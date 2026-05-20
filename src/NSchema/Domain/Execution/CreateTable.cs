using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record CreateTable(string SchemaName, Table Table) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
