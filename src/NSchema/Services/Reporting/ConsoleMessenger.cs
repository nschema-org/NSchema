using System.CommandLine;
using NSchema.Configuration;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// Builds an <see cref="IConsoleMessenger"/> straight from the parsed command line — no DI host.
/// </summary>
internal static class ConsoleMessenger
{
    public static IConsoleMessenger Create(ParseResult parseResult) =>
        Create(CommonOptions.Json.GetValueOrDefault(parseResult, false), ResolveVerbosity(parseResult));

    /// <summary>
    /// Builds an <see cref="IConsoleMessenger"/> for the resolved output format and verbosity.
    /// </summary>
    public static IConsoleMessenger Create(bool json, Verbosity verbosity) =>
        json ? new JsonConsoleMessenger(verbosity) : new SpectreConsoleMessenger(AnsiConsole.Console, verbosity);

    /// <summary>
    /// Resolves <c>--quiet</c> / <c>--verbose</c> to a single verbosity. The two flags are mutually exclusive:
    /// passing both is a usage error rather than a silent precedence.
    /// </summary>
    public static Verbosity ResolveVerbosity(ParseResult parseResult)
    {
        var quiet = CommonOptions.Quiet.GetValueOrDefault(parseResult, false);
        var verbose = CommonOptions.Verbose.GetValueOrDefault(parseResult, false);

        if (quiet && verbose)
        {
            throw new InvalidOperationException("--quiet and --verbose cannot be used together.");
        }

        return verbose ? Verbosity.Verbose : quiet ? Verbosity.Quiet : Verbosity.Normal;
    }
}
