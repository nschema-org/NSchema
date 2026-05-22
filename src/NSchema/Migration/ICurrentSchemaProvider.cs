using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Provides the current database schema for a given set of schema names.
/// </summary>
public interface ICurrentSchemaProvider
{
    /// <summary>
    /// Gets the current database schema for the specified schema names.
    /// </summary>
    /// <param name="schemaNames">The names of the schemas to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The current database schema.</returns>
    /// <remarks>If a schema name does not exist, it should not be included in the returned schema.</remarks>
    Task<DatabaseSchema> GetSchema(string[] schemaNames, CancellationToken cancellationToken = default);
}
