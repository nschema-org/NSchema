using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.State;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IStateCapturer"/>. The state store and the live provider are both optional (offline and
/// no-store configurations are valid), so they're resolved lazily rather than as hard constructor dependencies.
/// </summary>
/// <param name="options">Supplies the schema-name scope for the capture read.</param>
/// <param name="reporter">Presents capture progress.</param>
/// <param name="store">The state store to write the snapshot to, if any.</param>
/// <param name="live">The live provider to read the resulting schema from, if any.</param>
internal sealed class DefaultStateCapturer(
    IOptions<MigrationOptions> options,
    IMigrationReporter reporter,
    // Default values make these genuinely optional: MS DI only treats a parameter as optional when it has a
    // default, not from the nullable annotation alone. Without them, a no-store run fails to construct.
    ISchemaStateStore? store = null,
    [FromKeyedServices(ISchemaProvider.LiveCurrentSchemaProviderKey)] ISchemaProvider? live = null
) : IStateCapturer
{
    public async Task<bool> Capture(CancellationToken cancellationToken = default)
    {
        if (store == null)
        {
            return false;
        }

        // Capture the actual resulting database state, not the (possibly state-backed) planning source.
        if (live == null)
        {
            throw new InvalidOperationException("Capturing schema state requires a live current-schema provider. Register one (for example via UsePostgres).");
        }

        reporter.Info("Capturing schema state...");
        var schema = await live.GetSchema(options.Value.SchemaNames, cancellationToken);
        await store.Write(schema, cancellationToken);
        reporter.Info("Schema state captured.");
        return true;
    }
}
