using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// A current-state <see cref="ISchemaProvider"/> backed by an <see cref="ISchemaStateStore"/>.
/// </summary>
/// <remarks>
/// When no state exists yet (bootstrap), an empty <see cref="DatabaseSchema"/> is returned
/// so the first plan shows a full create.
/// The snapshot is returned as stored: state is written scoped to the managed schemas,
/// so reading it back already reflects exactly that scope.
/// </remarks>
/// <param name="store">The state store to read the snapshot from.</param>
internal sealed class StateBackedSchemaProvider(ISchemaStateStore store) : ISchemaProvider
{
    /// <inheritdoc />
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var schema = await store.Read(cancellationToken);
        return schema ?? DatabaseSchema.Create([]);
    }
}
