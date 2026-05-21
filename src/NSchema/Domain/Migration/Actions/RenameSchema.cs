namespace NSchema.Domain.Migration.Actions;

public sealed record RenameSchema(string OldName, string NewName) : SchemaAction
{
    public override bool IsDestructive => false;
}
