namespace NSchema.Migration.Actions;

public sealed record SetColumnDefault(
    string SchemaName,
    string TableName,
    string ColumnName,
    string? OldDefault,
    string? NewDefault
) : MigrationAction
{
    public override bool IsDestructive => false;
}
