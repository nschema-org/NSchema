namespace NSchema.Migration.Actions;

public sealed record SetSchemaComment(string SchemaName, string? OldComment, string? NewComment) : MigrationAction
{
    public override bool IsDestructive => false;
}
