using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record RunPreDeploymentScript(Script Script) : MigrationAction
{
    public override bool IsDestructive => false;
}
