using System.CommandLine;
using Spectre.Console;

namespace NSchema.Cli.Configuration;

/// <summary>
/// Builds the <see cref="IAnsiConsole"/>s the CLI renders through.
/// </summary>
internal static class ConsoleFactory
{
    /// <summary>
    /// Whether colored output should be suppressed: either <c>--no-color</c> was passed or <c>NO_COLOR</c> is set.
    /// </summary>
    public static bool IsColorDisabled(ParseResult parseResult) =>
        parseResult.GetValue(CliOptions.Common.NoColor)
        || Environment.GetEnvironmentVariable(EnvironmentVariables.NoColor) is not null;

    /// <summary>
    /// Creates a console over <paramref name="writer"/>.
    /// </summary>
    public static IAnsiConsole Create(TextWriter writer, bool colorDisabled) =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = colorDisabled ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
        });
}
