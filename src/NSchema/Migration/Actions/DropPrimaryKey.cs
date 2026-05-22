namespace NSchema.Migration.Actions;

public sealed record DropPrimaryKey(
    string SchemaName,
    string TableName,
    string PrimaryKeyName
) : MigrationAction
{
    public override bool IsDestructive => false;
}
