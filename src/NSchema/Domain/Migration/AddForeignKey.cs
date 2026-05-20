using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration;

public sealed record AddForeignKey(
    string SchemaName,
    string TableName,
    ForeignKey ForeignKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
