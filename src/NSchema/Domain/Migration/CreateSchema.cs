namespace NSchema.Domain.Migration;

public sealed record CreateSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
