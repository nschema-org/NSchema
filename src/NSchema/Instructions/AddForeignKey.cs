using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record AddForeignKey(
    string SchemaName,
    string TableName,
    ForeignKey ForeignKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}