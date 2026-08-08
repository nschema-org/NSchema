using NSchema.Commands;
using NSchema.Configuration;
using Spectre.Console;

namespace NSchema.Tests.Configuration;

public sealed class ConsoleFactoryTests : IDisposable
{
    private readonly string? _originalNoColor = Environment.GetEnvironmentVariable(EnvironmentVariables.NoColor);
    private readonly string? _originalColumns = Environment.GetEnvironmentVariable(EnvironmentVariables.Columns);
    private readonly string? _originalGitHubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");

    public ConsoleFactoryTests()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, null);
        Environment.SetEnvironmentVariable(EnvironmentVariables.Columns, null);
        // Spectre auto-detects CI hosts and re-enables ANSI; force that path on so the no-colour
        // decision is exercised under the same condition that broke in CI, not just on a bare local run.
        Environment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, _originalNoColor);
        Environment.SetEnvironmentVariable(EnvironmentVariables.Columns, _originalColumns);
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

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    // NO_COLOR disables colour whenever it is present and non-empty, whatever the value says — including "0".
    [InlineData("0")]
    public void NoColor_ResolvesTrue_WhenTheEnvironmentVariableIsPresent(string raw)
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, raw);
        var parseResult = RootCommand.Create().Parse(["plan"]);

        // Act / Assert
        CommonOptions.NoColor.GetValueOrDefault(parseResult, false).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoColor_ResolvesFalse_WhenTheEnvironmentVariableIsAbsentOrEmpty(string? raw)
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, raw);
        var parseResult = RootCommand.Create().Parse(["plan"]);

        // Act / Assert
        CommonOptions.NoColor.GetValueOrDefault(parseResult, false).ShouldBeFalse();
    }

    [Fact]
    public void CreateStandardError_MirrorsTheOutputConsolesColorDecision()
    {
        // Arrange — GITHUB_ACTIONS is set, so Spectre would re-enable colour on the redirected stderr stream.
        var output = ConsoleFactory.Create(new StringWriter(), colorDisabled: true);

        // Act
        var error = ConsoleFactory.CreateStandardError(output);

        // Assert
        error.Profile.Capabilities.ColorSystem.ShouldBe(ColorSystem.NoColors);
    }

    [Fact]
    public void Create_DoesNotWrapAtEightyColumns_WhenOutputIsRedirected()
    {
        // Arrange — a StringWriter is not a terminal, so Spectre would otherwise fall back to its 80-column width.
        var console = ConsoleFactory.Create(new StringWriter(), colorDisabled: true);

        // Assert — the redirected output gets a wide width so CI logs aren't hard-wrapped mid-line.
        console.Profile.Width.ShouldBeGreaterThan(80);
    }

    [Fact]
    public void Create_HonorsColumnsEnvironmentVariable_OverTheDetectedWidth()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentVariables.Columns, "123");

        // Act
        var console = ConsoleFactory.Create(new StringWriter(), colorDisabled: true);

        // Assert
        console.Profile.Width.ShouldBe(123);
    }

    [Fact]
    public void Create_IgnoresAnUnparseableColumnsValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentVariables.Columns, "not-a-number");

        // Act
        var console = ConsoleFactory.Create(new StringWriter(), colorDisabled: true);

        // Assert — a garbage COLUMNS value falls through to the redirected width rather than throwing.
        console.Profile.Width.ShouldBeGreaterThan(80);
    }
}
