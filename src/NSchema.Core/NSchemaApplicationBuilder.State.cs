using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSchema.State.Backends;
using NSchema.State.Locks.Backends;

namespace NSchema;

public partial class NSchemaApplicationBuilder
{
    /// <summary>
    /// Tracks whether the user has explicitly chosen a state lock (via <see cref="UseStateLock{T}()"/> and friends),
    /// so that configuring a state store never overrides an explicit choice, regardless of call order.
    /// </summary>
    private bool _explicitStateLock;

    /// <summary>
    /// Registers the <see cref="IDatabaseStateStore"/> used to persist and read schema snapshots.
    /// When the store type also implements <see cref="IStateLock"/>, it will be registered as the lock too.
    /// </summary>
    /// <typeparam name="T">The state store implementation to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateStore<T>() where T : class, IDatabaseStateStore
    {
        Services.Replace(ServiceDescriptor.Singleton<IDatabaseStateStore, T>());

        if (!_explicitStateLock)
        {
            // Co-locate the lock with the store: if the backend also locks, resolve the one instance for both seams.
            // Otherwise there is no lock, and operations run unlocked rather than against a placeholder.
            if (typeof(T).IsAssignableTo(typeof(IStateLock)))
            {
                Services.Replace(ServiceDescriptor.Singleton<IStateLock>(sp => (IStateLock)sp.GetRequiredService<IDatabaseStateStore>()));
            }
            else
            {
                Services.RemoveAll<IStateLock>();
            }
        }

        return this;
    }

    /// <summary>
    /// Registers an <see cref="IDatabaseStateStore"/> instance used to persist and read schema snapshots.
    /// When the instance also implements <see cref="IStateLock"/>, it is registered as the lock too.
    /// </summary>
    /// <param name="store">The state store instance.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateStore(IDatabaseStateStore store)
    {
        Services.Replace(ServiceDescriptor.Singleton(store));

        if (!_explicitStateLock)
        {
            if (store is IStateLock stateLock)
            {
                Services.Replace(ServiceDescriptor.Singleton(stateLock));
            }
            else
            {
                Services.RemoveAll<IStateLock>();
            }
        }

        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileDatabaseStateStore"/> that persists schema snapshots to a local file, and a matching <see cref="FileStateLock"/> at <c>&lt;path&gt;.lock</c>.
    /// </summary>
    /// <param name="path">The absolute or relative path of the state file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileState(string path)
    {
        Services.Configure<FileDatabaseStateStoreOptions>(o => o.Path = path);
        Services.Replace(ServiceDescriptor.Singleton<IDatabaseStateStore, FileDatabaseStateStore>());

        if (!_explicitStateLock)
        {
            Services.Configure<FileStateLockOptions>(o => o.Path = DeriveLockPath(path));
            Services.Replace(ServiceDescriptor.Singleton<IStateLock, FileStateLock>());
        }

        return this;
    }

    /// <summary>
    /// Registers an in-memory state store, which serves as the lock too, living only as long as the application
    /// instance. Intended for disposable databases like running tests in CI.
    /// </summary>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseEphemeralState() => UseStateStore(new EphemeralStateStore());

    /// <summary>
    /// Registers the <see cref="IStateLock"/> used to coordinate exclusive access to the state during the
    /// state-mutating operations (apply, destroy, refresh).
    /// </summary>
    /// <typeparam name="T">The state lock implementation to register.</typeparam>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateLock<T>() where T : class, IStateLock
    {
        _explicitStateLock = true;
        Services.Replace(ServiceDescriptor.Singleton<IStateLock, T>());
        return this;
    }

    /// <summary>
    /// Registers an <see cref="IStateLock"/> instance used to coordinate exclusive access to the state.
    /// </summary>
    /// <param name="stateLock">The state lock instance.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseStateLock(IStateLock stateLock)
    {
        _explicitStateLock = true;
        Services.Replace(ServiceDescriptor.Singleton(stateLock));
        return this;
    }

    /// <summary>
    /// Registers a <see cref="FileStateLock"/> that holds the lock as a file on the local filesystem.
    /// Intended for local development.
    /// </summary>
    /// <param name="path">The absolute or relative path of the lock file.</param>
    /// <returns>The application builder, for chaining.</returns>
    public NSchemaApplicationBuilder UseFileStateLock(string path)
    {
        _explicitStateLock = true;
        Services.Configure<FileStateLockOptions>(o => o.Path = path);
        Services.Replace(ServiceDescriptor.Singleton<IStateLock, FileStateLock>());
        return this;
    }

    /// <summary>
    /// Derives the lock-file path that pairs with a state file path (e.g. <c>schema.json</c> → <c>schema.json.lock</c>).
    /// </summary>
    private static string DeriveLockPath(string statePath) => statePath + ".lock";
}
