namespace NSchema.Domain.Migration.Actions;

public sealed record DropColumn(string SchemaName, string TableName, string ColumnName) : SchemaAction
{
    public override bool IsDestructive => true;
}
