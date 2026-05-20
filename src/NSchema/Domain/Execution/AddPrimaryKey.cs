using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record AddPrimaryKey(
    string SchemaName,
    string TableName,
    PrimaryKey PrimaryKey
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
