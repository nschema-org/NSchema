namespace NSchema.Migration.Actions;

public sealed record DropForeignKey(
    string SchemaName,
    string TableName,
    string ForeignKeyName
) : MigrationAction
{
    public override bool IsDestructive => false;
}
