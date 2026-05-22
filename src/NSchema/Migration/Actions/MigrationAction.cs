namespace NSchema.Migration.Actions;

public abstract record MigrationAction
{
    public abstract bool IsDestructive { get; }
}
