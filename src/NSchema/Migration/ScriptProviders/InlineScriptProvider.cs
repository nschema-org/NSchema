using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

internal sealed class InlineScriptProvider(IReadOnlyList<Script> pre, IReadOnlyList<Script> post) : IDeploymentScriptProvider
{
    public Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(pre);
    }

    public Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(post);
    }
}
