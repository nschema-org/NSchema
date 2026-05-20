using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record CreateIndex(
    string SchemaName,
    string TableName,
    TableIndex Index
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
