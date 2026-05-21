namespace NSchema.Migration.Actions;

public sealed record GrantSchemaUsage(string SchemaName, string Role) : SchemaAction
{
    public override bool IsDestructive => false;
}
