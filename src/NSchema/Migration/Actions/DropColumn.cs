namespace NSchema.Migration.Actions;

public sealed record DropColumn(string SchemaName, string TableName, string ColumnName) : MigrationAction
{
    public override bool IsDestructive => true;
}
