namespace NSchema.Domain.Migration.Actions;

public sealed record DropTable(string SchemaName, string TableName) : SchemaAction
{
    public override bool IsDestructive => true;
}
