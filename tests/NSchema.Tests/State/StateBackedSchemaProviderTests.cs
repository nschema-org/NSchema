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
    public void UseStateBackedCurrentSchema_RegistersAsCurrentProvider()
    {
        // Arrange
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseSchemaStateStore(_store).UseStateBackedCurrentSchema();
        using var app = builder.Build();

        // Act
        var current = app.Services.GetRequiredKeyedService<ISchemaProvider>(ISchemaProvider.CurrentSchemaProviderKey);

        // Assert
        current.ShouldBeOfType<StateBackedSchemaProvider>();
    }
}
