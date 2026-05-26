using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Provides a database schema, scoped to a set of schema names.
/// </summary>
/// <remarks>
/// A single shape is used for both desired-state and current-state providers, so the same implementation
/// (for example a live Postgres reader) can be plugged into either role, determined at registration time.
/// </remarks>
public interface ISchemaProvider
{
    /// <summary>
    /// DI key used to register the single current-state schema provider.
    /// </summary>
    internal const string CurrentSchemaProviderKey = "NSchema.Current";

    /// <summary>
    /// Gets the schema for the specified schema names.
    /// </summary>
    /// <param name="schemaNames">
    /// The names of the schemas to include. When null, the provider should return its full schema —
    /// declarative providers return everything they describe, while live-database providers return
    /// every schema they can see.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The schema for the specified schema names.</returns>
    /// <remarks>If a schema name does not exist, it should not be included in the returned schema.</remarks>
    Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
