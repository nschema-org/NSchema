namespace NSchema.Instructions;

public sealed record DropSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
