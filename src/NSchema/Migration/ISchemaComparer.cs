using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Defines a contract for comparing two database schemas and generating a migration plan that describes the necessary actions to transform the current schema into the desired schema.
/// </summary>
public interface ISchemaComparer
{
    /// <summary>
    /// Compares the current database schema with the desired database schema and generates a migration plan that describes the necessary actions to transform the current schema into the desired schema.
    /// </summary>
    /// <param name="current">The current database schema representing the existing state of the database.</param>
    /// <param name="desired">The desired database schema representing the target state of the database after migration.</param>
    /// <returns>A migration plan that outlines the steps required to migrate from the current schema to the desired schema.</returns>
    MigrationPlan Compare(DatabaseSchema current, DatabaseSchema desired);
}
