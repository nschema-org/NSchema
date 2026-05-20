using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record AddPrimaryKey(
    string SchemaName,
    string TableName,
    PrimaryKey PrimaryKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
