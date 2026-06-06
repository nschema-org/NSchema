using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Policies;
using NSchema.Services;
using NSchema.Sql;
using NSchema.Sql.Model;
using Spectre.Console.Testing;

namespace NSchema.Tests.Services;

public sealed class SpectreMigrationReporterTests
{
    private readonly TestConsole _out = new();
    private readonly TestConsole _error = new();
    private readonly IDiffRenderer _diffRenderer = Substitute.For<IDiffRenderer>();
    private readonly ISqlPlanRenderer _sqlPlanRenderer = Substitute.For<ISqlPlanRenderer>();
    private readonly SpectreMigrationReporter _sut;

    public SpectreMigrationReporterTests()
    {
        _out.Profile.Width = 200;
        _error.Profile.Width = 200;
        _sut = new SpectreMigrationReporter(_out, _error, _diffRenderer, _sqlPlanRenderer);
    }

    [Fact]
    public void Info_WritesMessageToOutput()
    {
        // Act
        _sut.Info("Planning complete.");

        // Assert
        _out.Output.ShouldContain("Planning complete.");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WritesMessageToErrorConsole()
    {
        // Exception
        var ex = new Exception("Boom!");

        // Act
        _sut.ReportException(ex);

        // Assert
        _error.Output.ShouldContain("Boom!");
        _out.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiff_FramesTheRenderedDiffInAPanel()
    {
        // Arrange
        _diffRenderer.Render(Arg.Any<MigrationDiff>()).Returns(
            "+ table app.widgets\n    + id bigint not null\nPlan: 1 to add, 0 to change, 0 to destroy.");

        // Act
        _sut.ReportDiff(diff: null!);

        // Assert
        _out.Output.ShouldContain("Plan");
        _out.Output.ShouldContain("table app.widgets");
        _out.Output.ShouldContain("1 to add");
    }

    [Fact]
    public void ReportDiff_DoesNotThrow_WhenRenderedTextContainsMarkupCharacters()
    {
        // Arrange — column array types contain square brackets, which are Spectre markup delimiters.
        _diffRenderer.Render(Arg.Any<MigrationDiff>()).Returns("~ tags type: text → text[]");

        // Act
        _sut.ReportDiff(diff: null!);

        // Assert
        _out.Output.ShouldContain("text[]");
    }

    [Fact]
    public void ReportSqlPlan_FramesTheRenderedSqlInAPanel()
    {
        // Arrange
        _sqlPlanRenderer.Render(Arg.Any<SqlPlan>()).Returns("SQL Preview:\n-- [1/1]\nCREATE TABLE app.widgets ();");

        // Act
        _sut.ReportSqlPlan(new SqlPlan([]));

        // Assert
        _out.Output.ShouldContain("SQL");
        _out.Output.ShouldContain("CREATE TABLE app.widgets");
    }

    [Fact]
    public void ReportDiagnostics_WritesPlaceholder_WhenEmpty()
    {
        // Act
        _sut.ReportDiagnostics(new PolicyDiagnostics());

        // Assert
        _out.Output.ShouldContain("No policy diagnostics.");
    }

    [Fact]
    public void ReportDiagnostics_WritesInfoToOutput()
    {
        // Arrange
        var diagnostics = new PolicyDiagnostics([new PolicyDiagnostic("style", "Naming hint", PolicyDiagnosticSeverity.Info)]);

        // Act
        _sut.ReportDiagnostics(diagnostics);

        // Assert
        _out.Output.ShouldContain("Naming hint");
        _error.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ReportDiagnostics_RoutesWarningsAndErrorsToErrorConsole()
    {
        // Arrange
        var diagnostics = new PolicyDiagnostics(
        [
            new PolicyDiagnostic("destructive", "Dropping column id", PolicyDiagnosticSeverity.Error),
        ]);

        // Act
        _sut.ReportDiagnostics(diagnostics);

        // Assert
        _error.Output.ShouldContain("Dropping column id");
        _error.Output.ShouldContain("destructive");
        _out.Output.ShouldBeEmpty();
    }
}
