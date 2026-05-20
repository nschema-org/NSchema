using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record RunPostDeploymentScript(DeploymentScript Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
