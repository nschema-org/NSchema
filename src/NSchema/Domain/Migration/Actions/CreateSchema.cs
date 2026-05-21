namespace NSchema.Domain.Migration.Actions;

public sealed record CreateSchema(string SchemaName) : SchemaAction
{
    public override bool IsDestructive => false;
}
