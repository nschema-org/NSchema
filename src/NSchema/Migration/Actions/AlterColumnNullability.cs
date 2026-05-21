namespace NSchema.Migration.Actions;

public sealed record AlterColumnNullability(
    string SchemaName,
    string TableName,
    string ColumnName,
    bool WasNullable,
    bool IsNullable
) : SchemaAction
{
    public override bool IsDestructive => true;
}
