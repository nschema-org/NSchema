using System.Reflection;
using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

/// <summary>
/// Provides migration scripts by loading a single embedded resource from a specified assembly that matches a given resource name.
/// </summary>
/// <param name="phase">The deployment phase for which the script should be provided.</param>
/// <param name="assembly">The assembly containing the embedded resource to be loaded as a migration script.</param>
/// <param name="resourceName">The name of the embedded resource in the assembly to be loaded as a migration script.</param>
/// <param name="name">An optional name to assign to the script; if not provided, a name will be derived from the resource name.</param>
internal sealed class EmbeddedResourceScriptProvider(DeploymentPhase phase, Assembly assembly, string resourceName, string? name = null) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> s_empty = [];

    public async Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Pre ? await Load(cancellationToken) : s_empty;

    public async Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Post ? await Load(cancellationToken) : s_empty;

    private async Task<IReadOnlyList<Script>> Load(CancellationToken cancellationToken)
    {
        var sql = await EmbeddedResource.Read(assembly, resourceName, cancellationToken);
        return [new Script(name ?? EmbeddedResource.DeriveName(resourceName), sql)];
    }
}
