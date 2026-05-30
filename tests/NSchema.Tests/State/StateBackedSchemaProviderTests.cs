using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.State;

namespace NSchema.Tests.State;

public sealed class StateBackedSchemaProviderTests
{
    private readonly ISchemaStateStore _store = Substitute.For<ISchemaStateStore>();

    [Fact]
    public async Task GetSchema_NoState_ReturnsEmptySchema()
    {
        // Arrange
        _store.Read(Arg.Any<CancellationToken>()).Returns((DatabaseSchema?)null);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema();

        // Assert: an empty schema makes a first-run plan show a full create.
        result.Schemas.ShouldBeEmpty();
        result.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSchema_WithState_ReturnsPersistedSchema()
    {
        // Arrange
        var persisted = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema();

        // Assert
        result.ShouldBeSameAs(persisted);
    }

    [Fact]
    public async Task GetSchema_EmptyScope_ReturnsPersistedSchema()
    {
        // Arrange: an empty scope means "return everything", same as null.
        var persisted = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema([]);

        // Assert
        result.ShouldBeSameAs(persisted);
    }

    [Fact]
    public async Task GetSchema_WithScope_FiltersToRequestedSchemas()
    {
        // Arrange: the store snapshots the whole database (e.g. includes the default "public"
        // schema), but a scoped read must only return the managed schemas — otherwise the diff
        // would plan to drop the unmanaged ones.
        var persisted = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("my_schema"),
            SchemaDefinition.Create("public")
        ]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema(["my_schema"]);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["my_schema"]);
    }

    [Fact]
    public async Task GetSchema_WithScope_MatchesSchemaNamesCaseInsensitively()
    {
        // Arrange: scope matching mirrors the comparer's OrdinalIgnoreCase comparison.
        var persisted = DatabaseSchema.Create([SchemaDefinition.Create("My_Schema")]);
        _store.Read(Arg.Any<CancellationToken>()).Returns(persisted);
        var sut = new StateBackedSchemaProvider(_store);

        // Act
        var result = await sut.GetSchema(["my_schema"]);

        // Assert
        result.Schemas.Select(s => s.Name).ShouldBe(["My_Schema"]);
    }

    [Fact]
    public void UseCurrentSchemaState_RegistersAsCurrentProvider()
    {
        // Arrange
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseStateStore(_store).UseCurrentSchemaState();
        using var app = builder.Build();

        // Act
        var current = app.Services.GetRequiredKeyedService<ISchemaProvider>(ISchemaProvider.CurrentSchemaProviderKey);

        // Assert
        current.ShouldBeOfType<StateBackedSchemaProvider>();
    }
}
