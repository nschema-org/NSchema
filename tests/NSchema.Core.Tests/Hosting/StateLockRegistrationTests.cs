using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.State.Backends;
using NSchema.State.Locks;
using NSchema.State.Locks.Backends;

namespace NSchema.Tests.Hosting;

public sealed class StateLockRegistrationTests
{
    private static IServiceProvider Build(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services;
    }

    [Fact]
    public void NoStore_RegistersNoLock()
    {
        // Act
        var services = Build(_ => { });

        // Assert
        // No backend, no lock: there is no placeholder to acquire — operations simply run unlocked.
        services.GetService<IStateLock>().ShouldBeNull();
    }

    [Fact]
    public void UseFileState_AutoRegistersAFileLockAtDerivedPath()
    {
        // Arrange
        var services = Build(b => b.UseFileState("state/schema.json"));

        // Act
        services.GetRequiredService<IStateLock>().ShouldBeOfType<FileStateLock>();

        // Assert
        services.GetRequiredService<IOptions<FileStateLockOptions>>().Value.Path.ShouldBe("state/schema.json.lock");
    }

    [Fact]
    public void EphemeralState_RegistersOneInMemoryInstanceForBothSeams()
    {
        // Arrange
        var services = Build(b => b.UseEphemeralState());

        // Act
        var store = services.GetRequiredService<IDatabaseStateStore>().ShouldBeOfType<EphemeralStateStore>();

        // Assert
        services.GetRequiredService<IStateLock>().ShouldBeSameAs(store);
    }

    [Fact]
    public void StoreThatAlsoLocks_RegistersTheSameInstanceForBothSeams()
    {
        // Arrange
        var services = Build(b => b.UseStateStore<LockingStore>());

        var store = services.GetRequiredService<IDatabaseStateStore>();

        // Act
        var stateLock = services.GetRequiredService<IStateLock>();

        // Assert
        stateLock.ShouldBeSameAs(store);
    }

    [Fact]
    public void StoreThatDoesNotLock_LeavesLockingOff()
    {
        // Act
        var services = Build(b => b.UseStateStore<StoreOnly>());

        // Assert
        // A store with no lock leaves IStateLock unregistered, so the state-mutating operations run unlocked.
        services.GetService<IStateLock>().ShouldBeNull();
    }

    [Fact]
    public void ExplicitLockBeforeStore_Wins()
    {
        var services = Build(b => b.UseStateLock<CustomLock>().UseFileState("schema.json"));

        services.GetRequiredService<IStateLock>().ShouldBeOfType<CustomLock>();
    }

    [Fact]
    public void ExplicitLockAfterStore_Wins()
    {
        var services = Build(b => b.UseFileState("schema.json").UseStateLock<CustomLock>());

        services.GetRequiredService<IStateLock>().ShouldBeOfType<CustomLock>();
    }

    private sealed class StoreOnly : IDatabaseStateStore
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) => Task.FromResult<ReadOnlyMemory<byte>?>(null);
        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class LockingStore : IDatabaseStateStore, IStateLock
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) => Task.FromResult<ReadOnlyMemory<byte>?>(null);
        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IStateLockHandle> Acquire(StateLockInfo lockInfo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask Release(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    // Implements IStateLock without Peek, exercising the default implementation.
    private sealed class CustomLock : IStateLock
    {
        public Task<IStateLockHandle> Acquire(StateLockInfo lockInfo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask Release(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
