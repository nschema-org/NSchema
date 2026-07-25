using NSchema.Model;
using NSchema.Model.Scripts;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Test-only conveniences that build a <see cref="ProjectDefinition"/> from a schema and a flat script list.
/// Production code never aggregates the two script kinds; a test often wants to declare "this schema plus these
/// scripts" without hand-routing each into its directives slice, so this splits the concrete kinds for it.
/// </summary>
internal static class TestProjects
{
    public static ProjectDefinition Project(Database database, IReadOnlyList<Script>? scripts = null)
    {
        scripts ??= [];
        var directives = new ProjectDirectives(
            ChangeScripts: [.. scripts.OfType<ChangeScript>()],
            DeploymentScripts: [.. scripts.OfType<DeploymentScript>()]);
        return new ProjectDefinition(database, directives);
    }
}
