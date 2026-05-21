namespace NSchema.Migration.Actions;

public sealed record DropPrimaryKey(
    string SchemaName,
    string TableName,
    string PrimaryKeyName
) : SchemaAction
{
    public override bool IsDestructive => false;
}
