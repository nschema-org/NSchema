namespace NSchema.Instructions;

public sealed record DropForeignKey(
    string SchemaName,
    string TableName,
    string ForeignKeyName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
