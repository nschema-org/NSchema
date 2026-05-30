using Microsoft.Extensions.Logging;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Tests.Hosting;

public sealed class DefaultMigrationReporterTests
{
    private readonly ILogger<DefaultMigrationReporter> _logger = Substitute.For<ILogger<DefaultMigrationReporter>>();
    private readonly IMigrationPlanRenderer _planRenderer = Substitute.For<IMigrationPlanRenderer>();

    private readonly DefaultMigrationReporter _sut;

    public DefaultMigrationReporterTests()
    {
        _sut = new DefaultMigrationReporter(_logger, _planRenderer);
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
    public void Status_WritesToStdoutAndLogsAtInformation()
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

    [Fact]
    public void ReportPlan_RendersPlanToStdout()
    {
        // Arrange
        var plan = new MigrationPlan([], DatabaseSchema.Create([]));
        _planRenderer.Render(plan).Returns("rendered diff");

        // Act
        var (stdout, stderr) = CaptureConsole(() => _sut.ReportPlan(plan));

        // Assert: the rendered diff followed by a blank separator line.
        stdout.ShouldBe("rendered diff" + Environment.NewLine + Environment.NewLine);
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void ReportPreview_WritesEachStatementToStdout()
    {
        // Arrange

        // Act
        var (stdout, stderr) = CaptureConsole(() => _sut.ReportPreview(["CREATE SCHEMA app", "CREATE TABLE app.users (id int)"]));

        // Assert
        stdout.ShouldBe(
            "CREATE SCHEMA app" + Environment.NewLine +
            "CREATE TABLE app.users (id int)" + Environment.NewLine);
        stderr.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_RoutesEachSeverityToCorrectStream()
    {
        // Arrange
        var diagnostics = new[]
        {
            new PolicyError("P1", "all good", PolicySeverity.Info),
            new PolicyError("P2", "be careful", PolicySeverity.Warning),
            new PolicyError("P3", "blocked", PolicySeverity.Error),
        };

        // Act
        var (stdout, stderr) = CaptureConsole(() => _sut.ReportDiagnostics(diagnostics));

        // Assert
        stdout.ShouldContain("P1: all good");
        stderr.ShouldContain("P2: be careful");
        stderr.ShouldContain("P3: blocked");
    }
}
