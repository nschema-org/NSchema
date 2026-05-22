namespace NSchema.Migration.Actions;

public sealed record RenameColumn(
    string SchemaName,
    string TableName,
    string OldName,
    string NewName
) : MigrationAction
{
    public override bool IsDestructive => false;
}
