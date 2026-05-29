using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private readonly MigrationOptions _options = new();
    private readonly IMigrationPipeline _pipeline = Substitute.For<IMigrationPipeline>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();

    private readonly NSchemaHost _sut;

    public NSchemaHostTests()
    {
        _sut = new NSchemaHost(Options.Create(_options), _lifetime, _pipeline);
    }

    [Fact]
    public async Task Execute_ApplyOperation_RunsApplyAndStops()
    {
        // Arrange
        _options.Operation = MigrationOperation.Apply;

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        await _pipeline.Received(1).Apply(Arg.Any<CancellationToken>());
        await _pipeline.DidNotReceive().Plan(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_PlanOperation_RunsPlanAndStops()
    {
        // Arrange
        _options.Operation = MigrationOperation.Plan;

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        await _pipeline.Received(1).Plan(Arg.Any<CancellationToken>());
        await _pipeline.DidNotReceive().Apply(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_StopsApplication_WhenPipelineThrows()
    {
        // Arrange
        _pipeline.Apply(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        await _sut.StartAsync(CancellationToken.None);

        // Act
        var act = async () => await _sut.ExecuteTask!;

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
        _lifetime.Received(1).StopApplication();
    }
}
