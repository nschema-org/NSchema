namespace NSchema.Instructions;

public sealed record CreateSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
