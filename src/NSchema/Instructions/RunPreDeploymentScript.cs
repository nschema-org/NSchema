using NSchema.Domain;

namespace NSchema.Instructions;

public sealed record RunPreDeploymentScript(DeploymentScript Script) : SchemaInstruction
{
    public override bool IsDestructive => false;
}