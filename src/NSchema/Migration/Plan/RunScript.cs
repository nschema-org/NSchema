using NSchema.Schema;

namespace NSchema.Migration.Plan;

/// <summary>
/// Represents running a script as part of the database migration process.
/// </summary>
/// <param name="Script">The SQL script to be executed.</param>
public sealed record RunScript(Script Script) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
