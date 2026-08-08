using System.CommandLine;

namespace NSchema.Commands;

/// <summary>
/// What a command's body is handed once the shared preamble has run.
/// </summary>
/// <typeparam name="TConfiguration">The command's configuration model.</typeparam>
/// <param name="App">The built application.</param>
/// <param name="Configuration">The resolved, validated configuration.</param>
/// <param name="ParseResult">The parsed command line, for arguments the configuration model does not carry.</param>
/// <param name="Environment">The environment the run targets, or <see langword="null"/> for the base configuration.</param>
internal sealed record CommandContext<TConfiguration>(
    CliApplication App,
    TConfiguration Configuration,
    ParseResult ParseResult,
    string? Environment
);
