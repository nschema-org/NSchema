namespace NSchema.Migration;

public class MigrationOptions
{
    public DestructiveActionPolicy DestructiveActionPolicy { get; set; } = DestructiveActionPolicy.Error;
    public bool DryRun { get; set; } = false;
}
