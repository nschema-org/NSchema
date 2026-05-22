using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record RunPostDeploymentScript(Script Script) : MigrationAction
{
    public override bool IsDestructive => false;
}
