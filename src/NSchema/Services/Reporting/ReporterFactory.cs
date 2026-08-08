using System.CommandLine;
using NSchema.Configuration;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// Builds the CLI's console reporter.
/// </summary>
internal static class ReporterFactory
{
    public static IConsoleReporter CreateReporter(ParseResult parseResult) => CreateReporter(
        ResolveFormat(parseResult),
        ResolveVerbosity(parseResult)
    );

    /// <summary>
    /// Builds an <see cref="IConsoleReporter"/> for the resolved output format and verbosity.
    /// </summary>
    public static IConsoleReporter CreateReporter(OutputFormat format, Verbosity verbosity) => format switch
    {
        OutputFormat.Json => new JsonConsoleReporter(verbosity),
        // Markdown owns stdout, so its narration goes through a stderr Spectre reporter — the same
        // results-on-stdout / logs-on-stderr split the JSON path uses — keeping the piped Markdown
        // (e.g. into $GITHUB_STEP_SUMMARY) uncontaminated.
        OutputFormat.Markdown => new MarkdownConsoleReporter(ConsoleFactory.CreateStandardError(AnsiConsole.Console), verbosity),
        _ => new SpectreConsoleReporter(AnsiConsole.Console, verbosity),
    };

    /// <summary>
    /// Resolves <c>--format</c> and the <c>--json</c> shorthand to a single <see cref="OutputFormat"/>.
    /// </summary>
    public static OutputFormat ResolveFormat(ParseResult parseResult)
    {
        var json = CommonOptions.Json.GetValueOrDefault(parseResult, false);
        var format = CommonOptions.Format.GetValueOrDefault(parseResult, OutputFormat.Text);

        return json ? OutputFormat.Json : format;
    }

    /// <summary>
    /// Resolves <c>--quiet</c> / <c>--verbose</c> to a single verbosity.
    /// </summary>
    public static Verbosity ResolveVerbosity(ParseResult parseResult)
    {
        var quiet = CommonOptions.Quiet.GetValueOrDefault(parseResult, false);
        var verbose = CommonOptions.Verbose.GetValueOrDefault(parseResult, false);

        return verbose ? Verbosity.Verbose : quiet ? Verbosity.Quiet : Verbosity.Normal;
    }
}
