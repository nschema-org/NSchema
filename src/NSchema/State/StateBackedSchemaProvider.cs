using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// A current-state <see cref="ISchemaProvider"/> backed by an <see cref="ISchemaStateStore"/>.
/// </summary>
/// <remarks>
/// When no state exists yet (bootstrap), an empty <see cref="DatabaseSchema"/> is returned
/// so the first plan shows a full create.
/// </remarks>
/// <param name="store">The state store to read the snapshot from.</param>
internal sealed class StateBackedSchemaProvider(ISchemaStateStore store) : ISchemaProvider
{
    /// <inheritdoc />
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var schema = await store.Read(cancellationToken);
        if (schema == null)
        {
            return DatabaseSchema.Create([]);
        }

        // Honor the ISchemaProvider contract: null/empty scope means "return everything".
        if (schemaNames is not { Length: > 0 })
        {
            return schema;
        }

        var set = new HashSet<string>(schemaNames, StringComparer.OrdinalIgnoreCase);
        return schema with { Schemas = schema.Schemas.Where(s => set.Contains(s.Name)).ToList() };
    }
}
