namespace NSchema.Domain.Schema;

public record DatabaseModel(
    IReadOnlyList<DatabaseSchema> Schemas,
    IReadOnlyList<DeploymentScript>? PreDeploymentScripts = null,
    IReadOnlyList<DeploymentScript>? PostDeploymentScripts = null
);
