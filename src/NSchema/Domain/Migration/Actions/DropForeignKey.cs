namespace NSchema.Domain.Migration.Actions;

public sealed record DropForeignKey(
    string SchemaName,
    string TableName,
    string ForeignKeyName
) : SchemaAction
{
    public override bool IsDestructive => false;
}
