using NSchema.Schema;

namespace NSchema.Migration.Actions;

public sealed record RunPreDeploymentScript(Script Script) : SchemaAction
{
    public override bool IsDestructive => false;
}
