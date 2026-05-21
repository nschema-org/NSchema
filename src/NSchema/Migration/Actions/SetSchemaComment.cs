namespace NSchema.Migration.Actions;

public sealed record SetSchemaComment(string SchemaName, string? OldComment, string? NewComment) : SchemaAction
{
    public override bool IsDestructive => false;
}
