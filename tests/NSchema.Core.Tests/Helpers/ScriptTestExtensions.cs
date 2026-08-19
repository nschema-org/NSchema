using NSchema.Diff.Domain;
using NSchema.Model.Scripts;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Helpers;

/// <summary>
/// Test-only conveniences that enumerate a project's or diff's scripts across both kinds. Production code
/// never aggregates the kinds — each lives in its own typed home — but an assertion often just wants "every
/// script the thing carries", so these flatten the two concrete streams for the test to inspect.
/// </summary>
internal static class ScriptTestExtensions
{
    public static IReadOnlyList<Script> AllScripts(this ProjectDefinition project) =>
        [.. project.Directives.ChangeScripts, .. project.Directives.DeploymentScripts];

    public static IReadOnlyList<Script> AllScripts(this DatabaseDiff diff) =>
        [.. diff.ChangeScripts(), .. diff.DeploymentScripts];
}
