using NSchema.Domain.Schema;

namespace NSchema.Domain.Migration;

public sealed record RunPostDeploymentScript(DeploymentScript Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
