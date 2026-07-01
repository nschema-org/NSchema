namespace NSchema.Services.Reporting;

/// <summary>
/// The CLI's output format, selected with <c>--format</c> (or the <c>--json</c> shorthand).
/// </summary>
internal enum OutputFormat
{
    /// <summary>
    /// Human-readable, colourised console output (the default).
    /// </summary>
    Text,

    /// <summary>
    /// Machine-readable newline-delimited JSON.
    /// </summary>
    Json,

    /// <summary>
    /// Markdown, for pasting into a PR comment or a CI job summary.
    /// </summary>
    Markdown,
}
