using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Provides the desired database schema for a migration process.
/// </summary>
public interface IDesiredSchemaProvider
{
    /// <summary>
    /// Gets the desired database schema that represents the target state of the database after migration.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The desired database schema.</returns>
    /// <remarks>
    /// The schema returned by this provider will be aggregated with any other desired schemas provided by other
    /// implementations of <see cref="IDesiredSchemaProvider"/> to form the complete desired state of the database.
    /// </remarks>
    Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default);
}
