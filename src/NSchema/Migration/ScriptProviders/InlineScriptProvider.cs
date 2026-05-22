using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

internal sealed class InlineScriptProvider(DeploymentPhase phase, Script script) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> Empty = [];

    public Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => Task.FromResult(phase == DeploymentPhase.Pre ? (IReadOnlyList<Script>)[script] : Empty);

    public Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => Task.FromResult(phase == DeploymentPhase.Post ? (IReadOnlyList<Script>)[script] : Empty);
}
