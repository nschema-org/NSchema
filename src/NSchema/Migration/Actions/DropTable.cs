namespace NSchema.Migration.Actions;

public sealed record DropTable(string SchemaName, string TableName) : MigrationAction
{
    public override bool IsDestructive => true;
}
