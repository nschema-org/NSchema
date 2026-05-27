using Microsoft.Extensions.Hosting;
using NSchema.Hosting;
using NSubstitute.ExceptionExtensions;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    private readonly IMigrationPipeline _pipeline = Substitute.For<IMigrationPipeline>();
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();

    private readonly NSchemaHost _sut;

    public NSchemaHostTests()
    {
        _sut = new NSchemaHost(_lifetime, _pipeline);
    }

    [Fact]
    public async Task Execute_StopsApplication_OnSuccess()
    {
        // Arrange

        // Act
        await _sut.StartAsync(CancellationToken.None);
        await _sut.ExecuteTask!;

        // Assert
        await _pipeline.Received(1).Run(Arg.Any<CancellationToken>());
        _lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_StopsApplication_WhenPipelineThrows()
    {
        // Arrange
        _pipeline.Run(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("boom"));
        await _sut.StartAsync(CancellationToken.None);

        // Act
        var act = async () => await _sut.ExecuteTask!;

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
        _lifetime.Received(1).StopApplication();
    }
}
