using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.State;

/// <summary>
/// A current-state <see cref="ISchemaProvider"/> that routes by operation: a <see cref="MigrationOperation.Plan"/>
/// reads the persisted state (offline), while an <see cref="MigrationOperation.Apply"/> reads the live database.
/// </summary>
/// <remarks>
/// This lets a single registration serve both operations — plan offline against the last applied state, then
/// apply against (and capture) the real database — without choosing the source per run.
/// </remarks>
/// <param name="liveProvider">The live current-state provider, used for an apply.</param>
/// <param name="store">The state store the plan-time snapshot is read from.</param>
/// <param name="options">Supplies the operation selected for the current run.</param>
internal sealed class AutoCurrentSchemaProvider(
    [FromKeyedServices(ISchemaProvider.LiveCurrentSchemaProviderKey)] ISchemaProvider liveProvider,
    ISchemaStateStore store,
    IOptions<MigrationOptions> options
) : ISchemaProvider
{
    private readonly StateBackedSchemaProvider _stateProvider = new(store);

    /// <inheritdoc />
    public Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var provider = options.Value.Operation == MigrationOperation.Plan
            ? _stateProvider
            : liveProvider;
        return provider.GetSchema(schemaNames, cancellationToken);
    }
}
