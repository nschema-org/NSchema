using Microsoft.Extensions.DependencyInjection;
using NSchema.State.Plugins;

namespace NSchema.Tests.State;

public sealed class StateStoreRegistrationTests
{
    private sealed class FakeStateStore : IDatabaseStateStore
    {
        public Task<Result<StoreReadResult>> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new StoreReadResult(null)));

        public Task<Result> Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private static IDatabaseStateStore? ResolveStore(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();
        return app.Services.GetService<IDatabaseStateStore>();
    }

    [Fact]
    public void AppState_ExposesTheManager_ConfiguredWhenAStoreIsRegistered()
    {
        // Arrange
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseStateStore<FakeStateStore>();
        using var app = builder.Build();

        // Assert
        app.State.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public void AppState_ExposesTheManager_UnconfiguredWithoutAStore()
    {
        // Arrange
        using var app = NSchemaApplication.CreateBuilder().Build();

        // Assert
        app.State.IsConfigured.ShouldBeFalse();
    }

    [Fact]
    public void UseFileState_RegistersFileStore()
    {
        // Act
        var store = ResolveStore(b => b.UseFileState("state.json"));

        // Assert
        store.ShouldBeOfType<FileDatabaseStateStore>();
    }

    [Fact]
    public void UseStateStore_Generic_RegistersStore()
    {
        // Act
        var store = ResolveStore(b => b.UseStateStore<FakeStateStore>());

        // Assert
        store.ShouldBeOfType<FakeStateStore>();
    }

    [Fact]
    public void UseStateStore_Instance_RegistersThatInstance()
    {
        // Arrange
        var instance = new FakeStateStore();

        // Act
        var store = ResolveStore(b => b.UseStateStore(instance));

        // Assert
        store.ShouldBeSameAs(instance);
    }

    [Fact]
    public void UseFileState_CalledTwice_LastOneWins()
    {
        var store = ResolveStore(b => b.UseStateStore<FakeStateStore>().UseFileState("state.json"));

        store.ShouldBeOfType<FileDatabaseStateStore>();
    }

    [Fact]
    public void NoStateStore_ResolvesToNull()
    {
        // Act
        var store = ResolveStore(_ => { });

        // Assert
        store.ShouldBeNull();
    }
}
