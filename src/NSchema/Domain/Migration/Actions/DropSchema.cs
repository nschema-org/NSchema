namespace NSchema.Domain.Migration.Actions;

public sealed record DropSchema(string SchemaName) : SchemaAction
{
    public override bool IsDestructive => true;
}
