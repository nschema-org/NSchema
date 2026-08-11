using System.CommandLine;
using NSchema.Commands;

namespace NSchema.Tests.Commands.Plan;

/// <summary>
/// A command that builds its application without going through <c>CommandRunner</c> still has to report what it
/// cannot build, rather than requiring a value the result does not carry. <c>plan show</c> stands for the shape:
/// <c>init</c>, <c>new</c> and the completion pair guard the same way, and it is the one that touches neither the
/// network nor the home directory whether the guard holds or not.
/// </summary>
public sealed class PlanShowCommandTests : IDisposable
{
    private readonly string _projectDirectory = Directory.CreateTempSubdirectory("nschema-plan-show-").FullName;
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    // The command applies --directory by changing the process working directory; restore it so tests stay hermetic.
    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_projectDirectory, recursive: true);
    }

    [Fact]
    public async Task PlanShow_WithAnUnreadableEditorConfig_ReportsItInsteadOfCrashing()
    {
        // Arrange — a severity the file's vocabulary has no word for, which fails the build.
        await File.WriteAllTextAsync(
            Path.Combine(_projectDirectory, ".editorconfig"),
            $"root = true{Environment.NewLine}{Environment.NewLine}[*]{Environment.NewLine}nschema_diagnostic.plugin-from-path.severity = bananas{Environment.NewLine}",
            TestContext.Current.CancellationToken);

        // The severities are resolved per schema file, so a project with none reads no .editorconfig at all.
        await File.WriteAllTextAsync(
            Path.Combine(_projectDirectory, "schema.sql"), "CREATE SCHEMA app;", TestContext.Current.CancellationToken);

        var plan = Path.Combine(_projectDirectory, "plan.json");
        await File.WriteAllTextAsync(plan, "{}", TestContext.Current.CancellationToken);

        // Set directly rather than with --directory: this command builds without going through the configuration
        // load that applies that option, so the project is only found by being where the process is.
        Directory.SetCurrentDirectory(_projectDirectory);

        var parseResult = NSchema.Commands.RootCommand.Create().Parse(["plan", "show", plan]);

        // Act — mirror Program.cs, with the default exception handler off so a crash reaches this test as one
        // rather than being folded into an exit code that looks like a clean failure.
        var invocation = new InvocationConfiguration { EnableDefaultExceptionHandler = false };
        var exitCode = await parseResult.InvokeAsync(invocation, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(ExitCodes.Error);
    }
}
