using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;
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
    /// Registers a <see cref="FileSchemaStateStore"/> that persists schema snapshots to a local file.
    /// </summary>
    /// <param name="path">The absolute or relative path of the state file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileStateStore(string path)
    {
        Services.Configure<FileSchemaStateStoreOptions>(o => o.Path = path);
        Services.AddSingleton<ISchemaStateStore, FileSchemaStateStore>();
        return this;
    }

    /// <summary>
    /// Registers the state store as the current-state schema source. Requires a state store to be registered.
    /// </summary>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateBackedCurrentSchema()
    {
        Services.AddKeyedSingleton<ISchemaProvider, StateBackedSchemaProvider>(ISchemaProvider.CurrentSchemaProviderKey);
        return this;
    }

    /// <summary>
    /// Reads the current schema from the state store when planning and from the live database when applying.
    /// </summary>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseCurrentSchemaAuto()
    {
        Services.AddKeyedSingleton<ISchemaProvider, AutoCurrentSchemaProvider>(ISchemaProvider.CurrentSchemaProviderKey);
        return this;
    }
}
