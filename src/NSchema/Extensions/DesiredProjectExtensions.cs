using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;

namespace NSchema.Extensions;

/// <summary>
/// Finds the run-once declarations — deployment scripts and data migrations — in a desired project, as the
/// name + body hash pairs the state ledger records.
/// </summary>
internal static class DesiredProjectExtensions
{
    extension(DesiredProject project)
    {
        /// <summary>
        /// The run-once declaration with the given name, or <see langword="null"/> when none exists.
        /// </summary>
        public ScriptHash? FindScript(string name) => project.All().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Every run-once declaration in the project, in declaration order (deployment scripts, then migrations).
        /// </summary>
        public IReadOnlyList<ScriptHash> All() =>
        [
            .. project.Scripts
                .Where(s => s.RunCondition == RunCondition.Once)
                .Select(s => new ScriptHash(s.Name, s.Hash)),
            .. project.Migrations
                .Where(m => m is { RunCondition: RunCondition.Once, Name: not null })
                .Select(m => new ScriptHash(m.Name!, m.Hash)),
        ];
    }
}
