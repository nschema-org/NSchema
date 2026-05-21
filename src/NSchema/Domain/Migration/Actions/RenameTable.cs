namespace NSchema.Domain.Migration.Actions;

public sealed record RenameTable(string SchemaName, string OldName, string NewName) : SchemaAction
{
    public override bool IsDestructive => false;
}
