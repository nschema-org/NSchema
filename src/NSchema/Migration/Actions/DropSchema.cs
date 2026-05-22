namespace NSchema.Migration.Actions;

public sealed record DropSchema(string SchemaName) : MigrationAction
{
    public override bool IsDestructive => true;
}
