using NSchema.Model;
using NSchema.Project.Domain.Directives;

namespace NSchema.Project;

/// <summary>
/// Provides the resolved project that represents the desired state of the database.
/// </summary>
public interface IProjectProvider
{
    /// <summary>
    /// Resolves the project, restricted to <paramref name="scope"/>.
    /// </summary>
    /// <param name="scope">The schemas to include.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved project, with any non-fatal findings raised while reading.</returns>
    ValueTask<Result<ProjectDefinition>> GetProject(PlanningScope scope, CancellationToken cancellationToken = default);
}
