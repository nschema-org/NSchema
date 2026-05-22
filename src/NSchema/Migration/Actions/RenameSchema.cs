namespace NSchema.Migration.Actions;

public sealed record RenameSchema(string OldName, string NewName) : MigrationAction
{
    public override bool IsDestructive => false;
}
