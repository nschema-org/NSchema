namespace NSchema.Domain.Migration;

public sealed record DropPrimaryKey(
    string SchemaName,
    string TableName,
    string PrimaryKeyName
) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
