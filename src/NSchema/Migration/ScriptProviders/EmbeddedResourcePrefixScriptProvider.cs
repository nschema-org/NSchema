using System.Reflection;
using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

/// <summary>
/// Provides migration scripts by loading embedded resources from a specified assembly that match a given resource name prefix.
/// </summary>
/// <param name="phase">The deployment phase for which the scripts should be provided.</param>
/// <param name="assembly">The assembly containing the embedded resources to be loaded as migration scripts.</param>
/// <param name="resourcePrefix">The prefix used to filter embedded resources in the assembly.</param>
internal sealed class EmbeddedResourcePrefixScriptProvider(DeploymentPhase phase, Assembly assembly, string resourcePrefix) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> s_empty = [];

    public async Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Pre ? await Load(cancellationToken) : s_empty;

    public async Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Post ? await Load(cancellationToken) : s_empty;

    private async Task<IReadOnlyList<Script>> Load(CancellationToken cancellationToken)
    {
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        var scripts = new List<Script>();
        foreach (var resourceName in resourceNames)
        {
            var sql = await EmbeddedResource.Read(assembly, resourceName, cancellationToken);
            scripts.Add(new Script(EmbeddedResource.DeriveName(resourceName), sql));
        }
        return scripts;
    }
}
