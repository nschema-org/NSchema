using NSchema.Project.Domain.Directives;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Computes the complete difference between what currently exists and the project: the structural schema
/// diff, with the pending script runs resolved onto it (deployment scripts that will fire, and change-event
/// scripts matched to their changes).
/// </summary>
internal interface IProjectComparer
{
    /// <summary>
    /// Compares <paramref name="current"/> against <paramref name="project"/>.
    /// </summary>
    /// <param name="current">What currently exists: the schema plus the recorded script executions.</param>
    /// <param name="project">The project: the schema plus the declared scripts.</param>
    /// <returns>The complete difference — always produced and carried, with any findings raised while computing it.</returns>
    Result<DatabaseDiff> Compare(CurrentState current, ProjectDefinition project);
}
