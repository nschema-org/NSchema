using NSchema.Model.Scripts;
using NSchema.Project.Model.Directives;
using NSchema.Services.Reporting;
using NSchema.State.Model;

namespace NSchema.Extensions;

/// <summary>
/// Finds the deployment scripts — the declarations the state ledger records — in a project or the recorded state,
/// by the name the user types (a bare name, or <c>schema.name</c> for a template-scoped script).
/// </summary>
internal static class ProjectScriptExtensions
{
    extension(ProjectDefinition project)
    {
        /// <summary>
        /// The deployment script with the given name, or <see langword="null"/> when none exists.
        /// </summary>
        public DeploymentScript? FindScript(string name) =>
            project.Directives.DeploymentScripts.FirstOrDefault(s => Matches(s.Address.Value, name));

        /// <summary>
        /// Every deployment script in the project, in declaration order, as name + body hash pairs.
        /// </summary>
        public IReadOnlyList<ScriptHashEntry> ScriptHashes() =>
        [
            .. project.Directives.DeploymentScripts.Select(s => new ScriptHashEntry(s.Address.Value, s.Hash.Value)),
        ];
    }

    extension(DatabaseState state)
    {
        /// <summary>
        /// The recorded execution for the named script, or <see langword="null"/> when none is recorded.
        /// </summary>
        public ScriptExecution? FindScript(string name) =>
            state.Scripts.FirstOrDefault(s => Matches(s.Script.Value, name));
    }

    private static bool Matches(string address, string name) =>
        string.Equals(address, name, StringComparison.OrdinalIgnoreCase);
}
