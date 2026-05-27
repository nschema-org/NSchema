using NSchema.Migration.Plan;

namespace NSchema.Migration.Sql;

/// <summary>
/// Defines an interface for planning SQL commands based on a migration plan.
/// </summary>
/// <remarks>
/// Implementations of this interface are responsible for taking a high-level migration plan,
/// which describes the desired changes to the database schema, and generating a detailed SQL plan that can be executed to apply those changes to the database.
/// </remarks>
public interface ISqlPlanner
{
    /// <summary>
    /// Generates a SQL plan based on the provided migration plan.
    /// </summary>
    /// <param name="plan">A migration plan containing the necessary steps to migrate a database schema.</param>
    /// <returns>A SQL plan that contains the ordered list of SQL commands to execute in order to apply the changes described in the migration plan.</returns>
    /// <remarks>
    /// The SQL plan contains the ordered list of SQL commands that need to be executed to migrate the database schema
    /// from its current state to the desired target state as described in the migration plan.
    /// </remarks>
    SqlPlan Plan(MigrationPlan plan);
}
