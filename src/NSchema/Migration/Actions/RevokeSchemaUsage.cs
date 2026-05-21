namespace NSchema.Migration.Actions;

public sealed record RevokeSchemaUsage(string SchemaName, string Role) : SchemaAction
{
    public override bool IsDestructive => true;
}
