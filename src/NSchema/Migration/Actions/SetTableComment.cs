namespace NSchema.Migration.Actions;

public sealed record SetTableComment(string SchemaName, string TableName, string? OldComment, string? NewComment) : SchemaAction
{
    public override bool IsDestructive => false;
}
