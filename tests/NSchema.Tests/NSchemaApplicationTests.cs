using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSchema.Hosting;
using NSchema.Migration;

namespace NSchema.Tests;

public sealed class NSchemaApplicationTests
{
    private readonly IMigrationPipeline _pipeline = Substitute.For<IMigrationPipeline>();

    private NSchemaApplication BuildApp(Action<NSchemaApplicationBuilder>? configure = null)
    {
        var builder = NSchemaApplication.CreateBuilder();
        // Replace the pipeline so the host exercises our substitute without needing schema providers.
        builder.Services.AddSingleton(_pipeline);
        configure?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public async Task Plan_RunsPlanOperation()
    {
        // Arrange
        using var app = BuildApp();

        // Act
        await app.Plan();

        // Assert
        await _pipeline.Received(1).Plan(Arg.Any<CancellationToken>());
        await _pipeline.DidNotReceive().Apply(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_RunsApplyOperation()
    {
        // Arrange
        using var app = BuildApp();

        // Act
        await app.Apply();

        // Assert
        await _pipeline.Received(1).Apply(Arg.Any<CancellationToken>());
        await _pipeline.DidNotReceive().Plan(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExplicitOperation_OverridesConfiguredOperation()
    {
        // Arrange: configured to Apply, but Plan() is invoked explicitly.
        using var app = BuildApp(b => b.RunOperation(MigrationOperation.Apply));

        // Act
        await app.Plan();

        // Assert
        await _pipeline.Received(1).Plan(Arg.Any<CancellationToken>());
        await _pipeline.DidNotReceive().Apply(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_UsesConfiguredOperation_WhenNoOverride()
    {
        // Arrange
        using var app = BuildApp(b => b.RunOperation(MigrationOperation.Plan));

        // Act
        await app.RunAsync();

        // Assert
        await _pipeline.Received(1).Plan(Arg.Any<CancellationToken>());
        await _pipeline.DidNotReceive().Apply(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondRun_Throws()
    {
        // Arrange
        using var app = BuildApp();
        await app.Plan();

        // Act
        var act = async () => await app.Apply();

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
