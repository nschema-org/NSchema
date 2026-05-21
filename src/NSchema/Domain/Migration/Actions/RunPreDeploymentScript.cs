using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration.Actions;

public sealed record RunPreDeploymentScript(Script Script) : SchemaAction
{
    public override bool IsDestructive => false;
}
