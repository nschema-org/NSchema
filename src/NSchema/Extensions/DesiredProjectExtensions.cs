using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Extensions;

/// <summary>
/// Finds the script — deployment scripts and data migrations — in a desired project, as the
/// name + body hash pairs the state ledger records.
/// </summary>
internal static class DesiredProjectExtensions
{
    extension(DesiredProject project)
    {
        /// <summary>
        /// The script with the given name, or <see langword="null"/> when none exists.
        /// </summary>
        public ScriptHash? FindScript(string name) => project.All().FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Every script in the project, in declaration order (deployment scripts, then migrations).
        /// </summary>
        public IReadOnlyList<ScriptHash> All() =>
        [
            .. project.Scripts.Select(s => new ScriptHash(s.Name, s.Hash)),
            .. project.Migrations.Where(m => m is { Name: not null }).Select(m => new ScriptHash(m.Name!, m.Hash)),
        ];
    }
}
