namespace NSchema.Domain.Migration.Actions;

public sealed record SetColumnDefault(
    string SchemaName,
    string TableName,
    string ColumnName,
    string? OldDefault,
    string? NewDefault
) : SchemaAction
{
    public override bool IsDestructive => false;
}
