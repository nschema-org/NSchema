namespace NSchema.Migration.Actions;

public sealed record SetIndexComment(string SchemaName, string TableName, string IndexName, string? OldComment, string? NewComment) : SchemaAction
{
    public override bool IsDestructive => false;
}
