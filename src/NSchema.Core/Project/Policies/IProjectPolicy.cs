using NSchema.Project.Domain.Directives;

namespace NSchema.Project.Policies;

/// <summary>
/// Validates a project against a set of rules.
/// </summary>
public interface IProjectPolicy
{
    /// <summary>
    /// Validates the given project against the rules defined by this policy.
    /// </summary>
    /// <param name="project">The project to validate against this policy.</param>
    IEnumerable<Diagnostic> Validate(ProjectDefinition project);
}
