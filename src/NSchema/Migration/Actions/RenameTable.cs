namespace NSchema.Migration.Actions;

public sealed record RenameTable(string SchemaName, string OldName, string NewName) : MigrationAction
{
    public override bool IsDestructive => false;
}
