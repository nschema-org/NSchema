namespace NSchema.Domain.Migration;

public sealed record DropSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
