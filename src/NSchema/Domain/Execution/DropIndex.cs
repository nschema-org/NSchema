namespace NSchema.Domain.Execution;

public sealed record DropIndex(
    string SchemaName,
    string TableName,
    string IndexName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
