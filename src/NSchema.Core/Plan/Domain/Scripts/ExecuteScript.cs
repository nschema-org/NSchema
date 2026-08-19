using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Plan.Domain.Scripts;

/// <summary>
/// Runs a declared script's raw SQL at its place in the plan.
/// </summary>
/// <param name="Script">The script to run.</param>
public sealed record ExecuteScript(Script Script) : MigrationAction
{
    /// <summary>
    /// The object this script relates to, or depends on, if any.
    /// </summary>
    public ObjectAddress? Anchor { get; init; }

    /// <summary>
    /// The script rendered verbatim.
    /// </summary>
    public SqlStatement Statement => new(Script.Sql, Script.RunOutsideTransaction);
}
