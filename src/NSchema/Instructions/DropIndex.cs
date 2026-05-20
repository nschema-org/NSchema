namespace NSchema.Instructions;

public sealed record DropIndex(
    string SchemaName,
    string TableName,
    string IndexName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
