using NSchema.Model;

namespace NSchema.Diff.Domain.Services;

/// <summary>
/// Defines a contract for comparing two database schemas and producing the structured <see cref="DatabaseDiff"/>
/// that describes the changes needed to transform the current schema into the desired schema.
/// </summary>
internal interface IDatabaseComparer
{
    /// <summary>
    /// Compares the current database schema with the desired database schema and produces the structured diff.
    /// </summary>
    /// <param name="current">The current database schema, aligned into the declared name-space.</param>
    /// <param name="desired">The desired database schema representing the target state of the database after migration.</param>
    /// <returns>The structured diff describing the changes between the two schemas.</returns>
    DatabaseDiff Compare(AlignedDatabase current, Database desired);
}
