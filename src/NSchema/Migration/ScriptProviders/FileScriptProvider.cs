using NSchema.Schema;

namespace NSchema.Migration.ScriptProviders;

/// <summary>
/// Provides migration scripts by loading a single SQL script from a specified file path.
/// </summary>
/// <param name="phase">The deployment phase for which the script should be provided.</param>
/// <param name="path">The file path of the SQL script to be loaded as a migration script.</param>
/// <param name="name">An optional name to assign to the script; if not provided, a name will be derived from the file name without extension.</param>
internal sealed class FileScriptProvider(DeploymentPhase phase, string path, string? name = null) : IDeploymentScriptProvider
{
    private static readonly IReadOnlyList<Script> s_empty = [];

    public async Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Pre ? await Load(cancellationToken) : s_empty;

    public async Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default)
        => phase == DeploymentPhase.Post ? await Load(cancellationToken) : s_empty;

    private async Task<IReadOnlyList<Script>> Load(CancellationToken cancellationToken)
    {
        var sql = await File.ReadAllTextAsync(path, cancellationToken);
        return [new Script(name ?? Path.GetFileNameWithoutExtension(path), sql)];
    }
}
