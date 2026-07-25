using Microsoft.Extensions.DependencyInjection;
using NSchema.Deployment;
using NSchema.Deployment.Backends;
using NSchema.Model;
using NSchema.Model.Schemas;

namespace NSchema.Tests.State;

public sealed class DatabaseProviderTests
{
    private static readonly Database _liveSchema = new Database { Schemas = [new Schema { Name = "live" }] };

    private static DatabaseProvider Create(IDatabaseIntrospector? online = null) => new(online);

    private sealed class FakeIntrospector : IDatabaseIntrospector
    {
        public ValueTask<Database> GetDatabase(PlanningScope scope, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_liveSchema);
    }

    [Fact]
    public async Task GetLive_ReturnsTheLiveDatabase()
    {
        // Arrange
        var sut = Create(online: new FakeIntrospector());

        // Act
        var result = await sut.GetDatabase(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.Require().ShouldBe(_liveSchema);
    }

    [Fact]
    public async Task GetLive_ReAppliesTheScope_WhenTheIntrospectorOverReturns()
    {
        // The fake ignores its scope entirely — the provider's re-filter is what keeps scoping honest.
        var sut = Create(online: new FakeIntrospector());

        var result = await sut.GetDatabase(PlanningScope.To(new SchemaAddress("other")), TestContext.Current.CancellationToken);

        result.Require().Schemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLive_WhenNotConfigured_Fails()
    {
        // Arrange
        var sut = Create();

        // Act
        var result = await sut.GetDatabase(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("live database provider");
    }

    // --- DI integration ---

    [Fact]
    public async Task UseDatabaseIntrospector_RegistersTheLiveSource()
    {
        // Arrange
        var builder = NSchemaApplication.CreateBuilder();
        builder.UseDatabaseIntrospector<FakeIntrospector>();
        using var app = builder.Build();
        var provider = app.Services.GetRequiredService<IDatabaseProvider>();

        // Act
        var live = await provider.GetDatabase(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        live.Require().ShouldBe(_liveSchema);
    }

    [Fact]
    public async Task GetLive_WithoutAnIntrospector_Fails()
    {
        // Arrange
        using var app = NSchemaApplication.CreateBuilder().Build();
        var provider = app.Services.GetRequiredService<IDatabaseProvider>();

        // Act
        var live = await provider.GetDatabase(PlanningScope.All, TestContext.Current.CancellationToken);

        // Assert
        live.IsFailure.ShouldBeTrue();
    }
}
