using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record AddColumn(string SchemaName, string TableName, Column Column) : SchemaInstruction
{
    public override bool IsDestructive => false;
}