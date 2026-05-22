namespace NSchema.Migration.Actions;

public sealed record CreateSchema(string SchemaName) : MigrationAction
{
    public override bool IsDestructive => false;
}
