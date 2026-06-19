using NSchema.Configuration;
using Spectre.Console;

namespace NSchema.Tests.Configuration;

public sealed class ConsoleFactoryTests : IDisposable
{
    private readonly string? _originalNoColor = Environment.GetEnvironmentVariable(EnvironmentVariables.NoColor);
    private readonly string? _originalGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");

    public ConsoleFactoryTests()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, null);
        // Spectre auto-detects CI hosts and re-enables ANSI; force that path on so the no-colour
        // decision is exercised under the same condition that broke in CI, not just on a bare local run.
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, _originalNoColor);
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", _originalGitHubActions);
    }

    [Fact]
    public void Create_ProducesAConsoleWithoutColor_WhenColorDisabled()
    {
        // Act
        var console = ConsoleFactory.Create(TextWriter.Null, colorDisabled: true);

        // Assert
        console.Profile.Capabilities.ColorSystem.ShouldBe(ColorSystem.NoColors);
    }

    [Fact]
    public void Create_EmitsNoEscapeSequencesForDecorations_WhenColorDisabled()
    {
        // Arrange
        var writer = new StringWriter();
        var console = ConsoleFactory.Create(writer, colorDisabled: true);

        // Act — [bold] is a decoration, not a colour, so NoColors alone would still emit ESC[1m.
        console.MarkupLine("[bold]Environment:[/] production");

        // Assert
        var output = writer.ToString();
        output.ShouldNotContain("\u001b");
        output.ShouldContain("Environment:");
    }
}
