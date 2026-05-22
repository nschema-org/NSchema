namespace NSchema.Migration.Actions;

public sealed record AlterColumnNullability(
    string SchemaName,
    string TableName,
    string ColumnName,
    bool WasNullable,
    bool IsNullable
) : MigrationAction
{
    public override bool IsDestructive => true;
}
