namespace NSchema.Migration.Actions;

public sealed record GrantSchemaUsage(string SchemaName, string Role) : MigrationAction
{
    public override bool IsDestructive => false;
}
