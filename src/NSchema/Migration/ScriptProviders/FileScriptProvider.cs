using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

internal sealed class FileScriptProvider(DeploymentPhase phase, string path, string? name = null) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> Empty = [];

    public async Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Pre ? await Load(cancellationToken) : Empty;

    public async Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Post ? await Load(cancellationToken) : Empty;

    private async Task<IReadOnlyList<Script>> Load(CancellationToken cancellationToken)
    {
        string sql = await File.ReadAllTextAsync(path, cancellationToken);
        return [new Script(name ?? Path.GetFileNameWithoutExtension(path), sql)];
    }
}
