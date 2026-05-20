namespace NSchema.Domain.Migration;

public sealed record DropForeignKey(
    string SchemaName,
    string TableName,
    string ForeignKeyName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
