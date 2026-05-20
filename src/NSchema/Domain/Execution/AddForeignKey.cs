using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record AddForeignKey(
    string SchemaName,
    string TableName,
    ForeignKey ForeignKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
