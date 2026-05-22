using NSchema.Schema;

namespace NSchema.Migration;

/// <summary>
/// Defines a contract for aggregating multiple database schemas into a single consolidated schema representation.
/// </summary>
public interface ISchemaAggregator
{
    /// <summary>
    /// Aggregates multiple database schemas into a single consolidated schema representation.
    /// </summary>
    /// <param name="schemas">An enumerable collection of DatabaseSchema objects that are to be aggregated.</param>
    /// <returns>A single schema object that represents the aggregated result of the input schemas.</returns>
    /// <remarks>
    /// This method takes an enumerable of DatabaseSchema objects and combines them into one cohesive schema that represents
    /// the overall structure and design of the database after considering all input schemas.
    /// The aggregation process may involve merging tables, resolving conflicts, and ensuring consistency across the combined schema.
    /// </remarks>
    DatabaseSchema Aggregate(IEnumerable<DatabaseSchema> schemas);
}
