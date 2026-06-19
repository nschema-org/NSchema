using Spectre.Console;

namespace NSchema.Configuration;

/// <summary>
/// Builds the <see cref="IAnsiConsole"/>s the CLI renders through.
/// </summary>
internal static class ConsoleFactory
{
    /// <summary>
    /// Creates a console over <paramref name="writer"/>.
    /// </summary>
    public static IAnsiConsole Create(TextWriter writer, bool colorDisabled) =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = colorDisabled ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
            Ansi = colorDisabled ? AnsiSupport.No : AnsiSupport.Detect,
        });
}
