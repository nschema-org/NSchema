namespace NSchema.Migration.Actions;

public sealed record DropTable(string SchemaName, string TableName) : SchemaAction
{
    public override bool IsDestructive => true;
}
