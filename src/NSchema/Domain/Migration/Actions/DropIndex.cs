namespace NSchema.Domain.Migration.Actions;

public sealed record DropIndex(
    string SchemaName,
    string TableName,
    string IndexName
) : SchemaAction
{
    public override bool IsDestructive => false;
}
