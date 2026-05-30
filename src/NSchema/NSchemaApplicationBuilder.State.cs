using Microsoft.Extensions.DependencyInjection;
using NSchema.State;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Registers the <see cref="ISchemaStateStore"/> used to persist and read schema snapshots.
    /// </summary>
    /// <typeparam name="T">The state store implementation to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSchemaStateStore<T>() where T : class, ISchemaStateStore
    {
        Services.AddSingleton<ISchemaStateStore, T>();
        return this;
    }

    /// <summary>
    /// Registers an <see cref="ISchemaStateStore"/> instance used to persist and read schema snapshots.
    /// </summary>
    /// <param name="store">The state store instance.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseSchemaStateStore(ISchemaStateStore store)
    {
        Services.AddSingleton(store);
        return this;
    }

    /// <summary>
    /// Registers a <see cref="LocalFileSchemaStateStore"/> that persists schema snapshots to a file on the
    /// local filesystem at the given path.
    /// </summary>
    /// <param name="path">The absolute or relative path of the state file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseLocalFileStateStore(string path)
    {
        Services.Configure<LocalFileSchemaStateStoreOptions>(o => o.Path = path);
        Services.AddSingleton<ISchemaStateStore, LocalFileSchemaStateStore>();
        return this;
    }
}
