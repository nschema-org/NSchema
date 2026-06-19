using Spectre.Console;

namespace NSchema.Configuration;

/// <summary>
/// Builds the <see cref="IAnsiConsole"/>s the CLI renders through.
/// </summary>
internal static class ConsoleFactory
{
    /// <summary>
    /// The rendered width to use when stdout is redirected (a pipe, a file, most CI runners). Spectre cannot query a
    /// terminal in that case and falls back to 80 columns, hard-wrapping every line; this wide, bounded width keeps
    /// output intact instead. It is bounded (rather than <see cref="int.MaxValue"/>) so width-spanning renderables
    /// don't allocate absurd buffers — but it's still wide enough that real schema/diff lines never wrap.
    /// </summary>
    private const int RedirectedWidth = 32767;

    /// <summary>
    /// Creates a console over <paramref name="writer"/>.
    /// </summary>
    public static IAnsiConsole Create(TextWriter writer, bool colorDisabled)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = colorDisabled ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect,
            Ansi = colorDisabled ? AnsiSupport.No : AnsiSupport.Detect,
            Enrichment = colorDisabled ? new ProfileEnrichment { UseDefaultEnrichers = false } : new ProfileEnrichment(),
        });

        console.Profile.Width = ResolveWidth(console.Profile);
        return console;
    }

    /// <summary>
    /// Resolves the rendered console width, layering the conventional <c>COLUMNS</c> override over Spectre's detection.
    /// </summary>
    private static int ResolveWidth(Profile profile)
    {
        // COLUMNS is the conventional knob for width; honour it whether or not we are attached to a terminal.
        var columns = Environment.GetEnvironmentVariable(EnvironmentVariables.Columns);
        if (int.TryParse(columns, out var width) && width > 0)
        {
            return width;
        }

        // A real terminal reports its own width via Spectre's detection — trust it. Otherwise stdout is redirected and
        // Spectre's 80-column fallback would hard-wrap the output, so use the wide redirected width instead.
        return profile.Out.IsTerminal ? profile.Width : RedirectedWidth;
    }
}
