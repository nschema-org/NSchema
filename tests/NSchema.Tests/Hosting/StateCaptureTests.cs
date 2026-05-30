using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.Hosting;

public sealed class StateCaptureTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private static IServiceProvider Services(ISchemaStateStore? store, ISchemaProvider? live)
    {
        var services = new ServiceCollection();
        if (store is not null)
        {
            services.AddSingleton(store);
        }
        if (live is not null)
        {
            services.AddKeyedSingleton(typeof(ISchemaProvider), ISchemaProvider.LiveCurrentSchemaProviderKey, live);
        }
        return services.BuildServiceProvider();
    }

    private StateCapture CreateSut(IServiceProvider services, MigrationOptions? options = null) =>
        new(services, Options.Create(options ?? new MigrationOptions()), _reporter);

    [Fact]
    public async Task Capture_NoStore_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(Services(store: null, live: null));

        // Act
        var result = await sut.Capture();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Capture_StoreButNoLiveProvider_Throws()
    {
        // Arrange
        var store = Substitute.For<ISchemaStateStore>();
        var sut = CreateSut(Services(store, live: null));

        // Act
        var act = () => sut.Capture();

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task Capture_ReadsLiveSchemaAndWritesToStore()
    {
        // Arrange
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var live = Substitute.For<ISchemaProvider>();
        live.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(schema);
        var store = Substitute.For<ISchemaStateStore>();
        var sut = CreateSut(Services(store, live));

        // Act
        var result = await sut.Capture();

        // Assert
        result.ShouldBeTrue();
        await store.Received(1).Write(schema, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_ScopesTheReadBySchemaNames()
    {
        // Arrange
        var live = Substitute.For<ISchemaProvider>();
        live.GetSchema(Arg.Any<string[]?>(), Arg.Any<CancellationToken>()).Returns(DatabaseSchema.Create([]));
        var store = Substitute.For<ISchemaStateStore>();
        var sut = CreateSut(Services(store, live), new MigrationOptions { SchemaNames = ["app"] });

        // Act
        await sut.Capture();

        // Assert
        await live.Received(1).GetSchema(
            Arg.Is<string[]?>(names => names != null && names.SequenceEqual(new[] { "app" })),
            Arg.Any<CancellationToken>());
    }
}
