using NSchema.Cli.Commands;
using NSchema.Cli.Configuration;
using Spectre.Console;

namespace NSchema.Cli.Tests.Configuration;

public sealed class ConsoleFactoryTests : IDisposable
{
    private readonly string? _originalNoColor = Environment.GetEnvironmentVariable(EnvironmentVariables.NoColor);

    public ConsoleFactoryTests() => Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, null);

    public void Dispose() => Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, _originalNoColor);

    [Fact]
    public void IsColorDisabled_ReturnsFalse_WhenNeitherFlagNorEnvSet()
    {
        // Arrange
        var parseResult = RootCommand.Create().Parse(["plan"]);

        // Act / Assert
        ConsoleFactory.IsColorDisabled(parseResult).ShouldBeFalse();
    }

    [Fact]
    public void IsColorDisabled_ReturnsTrue_WhenNoColorFlagSet()
    {
        // Arrange
        var parseResult = RootCommand.Create().Parse(["plan", "--no-color"]);

        // Act / Assert
        ConsoleFactory.IsColorDisabled(parseResult).ShouldBeTrue();
    }

    [Fact]
    public void IsColorDisabled_ReturnsTrue_WhenNoColorEnvSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, "1");
        var parseResult = RootCommand.Create().Parse(["plan"]);

        // Act / Assert
        ConsoleFactory.IsColorDisabled(parseResult).ShouldBeTrue();
    }

    [Fact]
    public void Create_ProducesAConsoleWithoutColor_WhenColorDisabled()
    {
        // Act
        var console = ConsoleFactory.Create(TextWriter.Null, colorDisabled: true);

        // Assert
        console.Profile.Capabilities.ColorSystem.ShouldBe(ColorSystem.NoColors);
    }
}
