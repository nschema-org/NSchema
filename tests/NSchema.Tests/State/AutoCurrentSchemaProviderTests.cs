using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class AutoCurrentSchemaProviderTests
{
    private static readonly DatabaseSchema LiveSchema = DatabaseSchema.Create([SchemaDefinition.Create("live")]);
    private static readonly DatabaseSchema StateSchema = DatabaseSchema.Create([SchemaDefinition.Create("state")]);

    private sealed class LiveProvider : ISchemaProvider
    {
        public Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(LiveSchema);
    }

    private sealed class StateStore : ISchemaStateStore
    {
        public Task<DatabaseSchema?> Read(CancellationToken cancellationToken = default) => Task.FromResult<DatabaseSchema?>(StateSchema);
        public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task GetSchema_PlanOperation_ReadsFromState()
    {
        // Arrange
        var live = Substitute.For<ISchemaProvider>();
        var store = Substitute.For<ISchemaStateStore>();
        store.Read(Arg.Any<CancellationToken>()).Returns(StateSchema);
        var options = Options.Create(new MigrationOptions { Operation = MigrationOperation.Plan });
        var sut = new AutoCurrentSchemaProvider(live, store, options);

        // Act
        var result = await sut.GetSchema();

        // Assert
        result.ShouldBeSameAs(StateSchema);
        await live.DidNotReceive().GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSchema_ApplyOperation_ReadsFromLive()
    {
        // Arrange
        var live = Substitute.For<ISchemaProvider>();
        live.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(LiveSchema);
        var store = Substitute.For<ISchemaStateStore>();
        var options = Options.Create(new MigrationOptions { Operation = MigrationOperation.Apply });
        var sut = new AutoCurrentSchemaProvider(live, store, options);

        // Act
        var result = await sut.GetSchema();

        // Assert
        result.ShouldBeSameAs(LiveSchema);
        await store.DidNotReceive().Read(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UseAutoCurrentSchema_WiresRoutingThroughDependencyInjection()
    {
        // Arrange
        var builder = NSchemaApplication.CreateBuilder();
        builder
            .UseCurrentSchema<LiveProvider>()
            .UseSchemaStateStore(new StateStore())
            .UseCurrentSchemaAuto();
        using var app = builder.Build();

        var current = app.Services.GetRequiredKeyedService<ISchemaProvider>(ISchemaProvider.CurrentSchemaProviderKey);
        var options = app.Services.GetRequiredService<IOptions<MigrationOptions>>();

        // Assert: the effective current provider is the auto router...
        current.ShouldBeOfType<AutoCurrentSchemaProvider>();

        // ...and it routes by the configured operation.
        options.Value.Operation = MigrationOperation.Plan;
        (await current.GetSchema()).Schemas.ShouldHaveSingleItem().Name.ShouldBe("state");

        options.Value.Operation = MigrationOperation.Apply;
        (await current.GetSchema()).Schemas.ShouldHaveSingleItem().Name.ShouldBe("live");
    }

    [Fact]
    public void UseCurrentSchema_ResolvesAsEffectiveCurrentProvider()
    {
        // Arrange: with no override, the effective current provider is the live one.
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseCurrentSchema<LiveProvider>();
        using var app = builder.Build();

        // Act
        var current = app.Services.GetRequiredKeyedService<ISchemaProvider>(ISchemaProvider.CurrentSchemaProviderKey);

        // Assert
        current.ShouldBeOfType<LiveProvider>();
    }
}
