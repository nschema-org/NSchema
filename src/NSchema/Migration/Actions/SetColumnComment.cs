namespace NSchema.Migration.Actions;

public sealed record SetColumnComment(string SchemaName, string TableName, string ColumnName, string? OldComment, string? NewComment) : MigrationAction
{
    public override bool IsDestructive => false;
}
