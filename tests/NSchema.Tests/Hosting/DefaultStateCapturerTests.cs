using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.Hosting;

public sealed class DefaultStateCapturerTests
{
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private DefaultStateCapturer CreateSut(ISchemaStateStore? store, ISchemaProvider? live, MigrationOptions? options = null) =>
        new(Options.Create(options ?? new MigrationOptions()), _reporter, store, live);

    [Fact]
    public async Task Capture_NoStore_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut(store: null, live: null);

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
        var sut = CreateSut(store, live: null);

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
        var sut = CreateSut(store, live);

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
        var sut = CreateSut(store, live, new MigrationOptions { SchemaNames = ["app"] });

        // Act
        await sut.Capture();

        // Assert
        await live.Received(1).GetSchema(
            Arg.Is<string[]?>(names => names != null && names.SequenceEqual(new[] { "app" })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Resolves_WhenNoStoreOrLiveProviderRegistered()
    {
        // The store and live provider are optional: an app with neither must still resolve the capturer
        // (so a no-store apply works). This relies on DI injecting null for the unregistered dependencies.
        var builder = NSchemaApplication.CreateBuilder();
        using var app = builder.Build();

        var capturer = app.Services.GetRequiredService<IStateCapturer>();

        capturer.ShouldBeOfType<DefaultStateCapturer>();
    }
}
