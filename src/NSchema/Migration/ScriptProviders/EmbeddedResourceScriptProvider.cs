using System.Reflection;
using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

internal sealed class EmbeddedResourceScriptProvider(
    DeploymentPhase phase,
    Assembly assembly,
    string resourceName,
    string? name = null
) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> Empty = [];

    public async Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Pre ? await Load(cancellationToken) : Empty;

    public async Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Post ? await Load(cancellationToken) : Empty;

    private async Task<IReadOnlyList<Script>> Load(CancellationToken cancellationToken)
    {
        string sql = await EmbeddedResource.Read(assembly, resourceName, cancellationToken);
        return [new Script(name ?? EmbeddedResource.DeriveName(resourceName), sql)];
    }
}
