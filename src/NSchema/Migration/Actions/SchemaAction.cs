namespace NSchema.Migration.Actions;

public abstract record SchemaAction
{
    public abstract bool IsDestructive { get; }
}
