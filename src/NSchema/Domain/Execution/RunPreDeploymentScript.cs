using NSchema.Domain.Schema;

namespace NSchema.Domain.Execution;

public sealed record RunPreDeploymentScript(DeploymentScript Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
