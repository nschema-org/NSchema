namespace NSchema.Migration.Actions;

public sealed record SetTableComment(string SchemaName, string TableName, string? OldComment, string? NewComment) : MigrationAction
{
    public override bool IsDestructive => false;
}
