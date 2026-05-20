using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record CreateIndex(
    string SchemaName,
    string TableName,
    TableIndex Index
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}