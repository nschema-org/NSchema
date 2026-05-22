using System.Reflection;
using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

internal sealed class EmbeddedResourcePrefixScriptProvider(
    DeploymentPhase phase,
    Assembly assembly,
    string resourcePrefix
) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> Empty = [];

    public async Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Pre ? await Load(cancellationToken) : Empty;

    public async Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Post ? await Load(cancellationToken) : Empty;

    private async Task<IReadOnlyList<Script>> Load(CancellationToken cancellationToken)
    {
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        var scripts = new List<Script>();
        foreach (string resourceName in resourceNames)
        {
            string sql = await EmbeddedResource.Read(assembly, resourceName, cancellationToken);
            scripts.Add(new Script(EmbeddedResource.DeriveName(resourceName), sql));
        }
        return scripts;
    }
}
