using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Provides pre-deployment and post-deployment scripts for a migration plan.
/// </summary>
public interface IDeploymentScriptProvider
{
    /// <summary>
    /// Gets the pre-deployment and post-deployment scripts for a migration plan.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The pre-deployment scripts.</returns>
    Task<IReadOnlyList<Script>> GetPreDeploymentScripts(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the post-deployment scripts for a migration plan.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The post-deployment scripts.</returns>
    Task<IReadOnlyList<Script>> GetPostDeploymentScripts(CancellationToken cancellationToken = default);
}
