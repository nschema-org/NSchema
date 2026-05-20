using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record AddColumn(string SchemaName, string TableName, Column Column) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
