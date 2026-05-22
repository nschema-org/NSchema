namespace NSchema.Migration.Actions;

public sealed record RevokeSchemaUsage(string SchemaName, string Role) : MigrationAction
{
    public override bool IsDestructive => true;
}
