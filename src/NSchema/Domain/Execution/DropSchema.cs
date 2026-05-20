namespace NSchema.Domain.Execution;

public sealed record DropSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => true;
}
