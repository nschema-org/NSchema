using NSchema.Cli.Configuration;
using Spectre.Console;

namespace NSchema.Cli.Tests.Configuration;

public sealed class ConsoleFactoryTests : IDisposable
{
    private readonly string? _originalNoColor = Environment.GetEnvironmentVariable(EnvironmentVariables.NoColor);

    public ConsoleFactoryTests() => Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, null);

    public void Dispose() => Environment.SetEnvironmentVariable(EnvironmentVariables.NoColor, _originalNoColor);

    [Fact]
    public void Create_ProducesAConsoleWithoutColor_WhenColorDisabled()
    {
        // Act
        var console = ConsoleFactory.Create(TextWriter.Null, colorDisabled: true);

        // Assert
        console.Profile.Capabilities.ColorSystem.ShouldBe(ColorSystem.NoColors);
    }
}
