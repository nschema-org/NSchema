using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record RunPostDeploymentScript(Script Script) : SchemaAction
{
    public override bool IsDestructive => false;
}
