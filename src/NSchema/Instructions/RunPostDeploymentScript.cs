using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record RunPostDeploymentScript(DeploymentScript Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}
