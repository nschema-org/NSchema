namespace NSchema.Migration.Actions;

public sealed record RenameColumn(
    string SchemaName,
    string TableName,
    string OldName,
    string NewName
) : SchemaAction
{
    public override bool IsDestructive => false;
}
