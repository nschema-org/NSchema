using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.Plan.Policies;
using NSchema.Project.Domain.Directives;
using NSchema.Project.Policies;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Lets policy tests validate the bare artifact part they exercise: a diff stands in for a plan with no
/// statements, a schema for a project with no scripts.
/// </summary>
internal static class PolicyTestExtensions
{
    public static IEnumerable<Diagnostic> Validate(this IPlanPolicy policy, DatabaseDiff diff) =>
        policy.Validate(new MigrationPlan(diff, []));

    public static IEnumerable<Diagnostic> Validate(this IProjectPolicy policy, Database schema) =>
        policy.Validate(new ProjectDefinition(schema));
}
