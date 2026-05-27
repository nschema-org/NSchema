using Microsoft.Extensions.Logging;
using NSchema.Hosting;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterTests
{
    private readonly ILogger<DefaultMigrationReporter> _logger = Substitute.For<ILogger<DefaultMigrationReporter>>();

    private readonly DefaultMigrationReporter _sut;

    public DefaultMigrationReporterTests()
    {
        _sut = new DefaultMigrationReporter(_logger);
    }

    private static (string Out, string Err) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            action();
            return (stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Fact]
    public void Info_WritesToStdoutAndLogsAtInformation()
    {
        // Arrange

        // Act
        var (stdout, stderr) = CaptureConsole(() => _sut.Info("hello"));

        // Assert
        stdout.ShouldBe("hello" + Environment.NewLine);
        stderr.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("hello")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Warn_WritesToStderrAndLogsAtWarning()
    {
        // Arrange

        // Act
        var (stdout, stderr) = CaptureConsole(() => _sut.Warn("careful"));

        // Assert
        stderr.ShouldBe("careful" + Environment.NewLine);
        stdout.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("careful")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public void Error_WritesToStderrAndLogsAtError()
    {
        // Arrange

        // Act
        var (stdout, stderr) = CaptureConsole(() => _sut.Error("boom"));

        // Assert
        stderr.ShouldBe("boom" + Environment.NewLine);
        stdout.ShouldBeEmpty();
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("boom")),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
