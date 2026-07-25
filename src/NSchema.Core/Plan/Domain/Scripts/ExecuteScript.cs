using NSchema.Model.Scripts;

namespace NSchema.Plan.Domain.Scripts;

/// <summary>
/// Runs a declared script's raw SQL at its place in the plan.
/// </summary>
/// <param name="Script">The script to run.</param>
public sealed record ExecuteScript(Script Script) : MigrationAction
{
    /// <summary>
    /// The script rendered verbatim.
    /// </summary>
    public SqlStatement Statement => new(Script.Sql, Script.RunOutsideTransaction);
}
