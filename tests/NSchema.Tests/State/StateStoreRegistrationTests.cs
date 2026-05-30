using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class StateStoreRegistrationTests
{
    private sealed class FakeStateStore : ISchemaStateStore
    {
        public Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult<DatabaseSchema?>(null);

        public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static ISchemaStateStore? ResolveStore(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();
        return app.Services.GetService<ISchemaStateStore>();
    }

    [Fact]
    public void UseLocalFileStateStore_RegistersLocalFileStore()
    {
        // Act
        var store = ResolveStore(b => b.UseLocalFileStateStore("state.json"));

        // Assert
        store.ShouldBeOfType<LocalFileSchemaStateStore>();
    }

    [Fact]
    public void UseSchemaStateStore_Generic_RegistersStore()
    {
        // Act
        var store = ResolveStore(b => b.UseSchemaStateStore<FakeStateStore>());

        // Assert
        store.ShouldBeOfType<FakeStateStore>();
    }

    [Fact]
    public void UseSchemaStateStore_Instance_RegistersThatInstance()
    {
        // Arrange
        var instance = new FakeStateStore();

        // Act
        var store = ResolveStore(b => b.UseSchemaStateStore(instance));

        // Assert
        store.ShouldBeSameAs(instance);
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
