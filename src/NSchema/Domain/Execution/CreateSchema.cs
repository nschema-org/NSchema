namespace NSchema.Domain.Execution;

public sealed record CreateSchema(string SchemaName) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
