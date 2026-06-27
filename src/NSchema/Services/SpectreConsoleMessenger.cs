using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Policies;
using NSchema.State.Model;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// Renders line-level narration and outcomes with Spectre.Console.
/// </summary>
internal class SpectreConsoleMessenger : IConsoleMessenger
{
    protected readonly IAnsiConsole Out;
    protected readonly IAnsiConsole Error;
    private readonly Verbosity _verbosity;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsoleMessenger(IAnsiConsole console, Verbosity verbosity)
        : this(console, CreateStandardErrorConsole(console), verbosity) { }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsoleMessenger(IAnsiConsole output, IAnsiConsole error, Verbosity verbosity)
    {
        Out = output;
        Error = error;
        _verbosity = verbosity;
    }

    public void Report(MessageKind kind, string message) => WriteLine(kind, Markup.Escape(message));

    public void Announce(ConsoleMessage message) => WriteLine(MessageKind.Announcement, message.Styled);

    public void Progress(ConsoleMessage message) => WriteLine(MessageKind.Progress, message.Styled);

    public void Success(ConsoleMessage message) => WriteLine(MessageKind.Success, message.Styled);

    public void Warn(ConsoleMessage message) => WriteLine(MessageKind.Warning, message.Styled);

    private void WriteLine(MessageKind kind, string body)
    {
        if (!_verbosity.ShouldShow(kind))
        {
            return;
        }

        var (console, markup) = kind switch
        {
            MessageKind.Success => (Out, $"[green]:check_mark: {body}[/]"),
            MessageKind.Warning => (Error, $"[yellow]:warning: {body}[/]"),
            MessageKind.Progress => (Out, $"[grey]{body}[/]"),
            // Dimmed and italicised so verbose detail reads as secondary to the run narration.
            MessageKind.Verbose => (Out, $"[grey italic]{body}[/]"),
            _ => (Out, body),
        };

        console.MarkupLine(markup);
    }

    public void Detail(string message) => Out.MarkupLine($"[grey]  {Markup.Escape(message)}[/]");

    public void Detail(ConsoleMessage message) => Out.MarkupLine($"[grey]  {message.Styled}[/]");

    public void ReportLockStatus(StateLockInfo? info)
    {
        if (info is null)
        {
            Report(MessageKind.Success, "The state is not locked.");
            return;
        }

        Warn($"The state is locked by {info.Who} (operation '{info.Operation}', since {info.CreatedUtc:u}).");
        Detail($"Lock ID: {info.Id}");

        // Surface a manual hold's lifetime, and flag it once past — but NSchema never auto-breaks an expired lock.
        if (info.ExpiresUtc is { } expires)
        {
            if (expires <= DateTimeOffset.UtcNow)
            {
                Detail($"Expires: {expires:u} (expired)");
            }
            else
            {
                Detail($"Expires: {expires:u}");
            }
        }

        Detail($"Release it, once you're sure no operation is still running, with: nschema lock release {info.Id}");
    }

    public void ReportException(Exception exception)
    {
        // A policy violation carries structured diagnostics; show the table first, then the headline error.
        if (exception is PolicyViolationException violation)
        {
            RenderDiagnostics(Error, violation.Errors);
        }

        Error.MarkupLineInterpolated($"[red]Error:[/] {exception.Message}");
    }

    public void ReportEnvironment(string? environment)
    {
        // Prints which environment a run is targeting, so a command run against (say) production is unmistakable.
        if (environment is null)
        {
            return;
        }

        Out.MarkupLineInterpolated($"[bold]Environment:[/] [yellow]{environment}[/]");
        Out.WriteLine();
    }

    // Renders a policy-diagnostic table. Shared by ReportException (here) and the presenter's ReportDiagnostics.
    protected static void RenderDiagnostics(IAnsiConsole console, IReadOnlyList<PolicyDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            console.MarkupLine("[grey]No policy diagnostics.[/]");
            console.WriteLine();
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .Title("Policy diagnostics")
            .AddColumn("Severity")
            .AddColumn("Policy")
            .AddColumn("Message");

        foreach (var diagnostic in diagnostics)
        {
            table.AddRow(
                new Markup(SeverityLabel(diagnostic.Severity)),
                new Markup(Markup.Escape(diagnostic.PolicyName)),
                new Markup(Markup.Escape(diagnostic.Message)));
        }

        console.Write(table);
        console.WriteLine();
    }

    private static string SeverityLabel(PolicyDiagnosticSeverity severity) => severity switch
    {
        PolicyDiagnosticSeverity.Error => "[red]error[/]",
        PolicyDiagnosticSeverity.Warning => "[yellow]warning[/]",
        _ => "[grey]info[/]",
    };

    // Mirror the output console's color decision (which already reflects --no-color / NO_COLOR) onto stderr.
    private static IAnsiConsole CreateStandardErrorConsole(IAnsiConsole output) =>
        ConsoleFactory.Create(Console.Error, output.Profile.Capabilities.ColorSystem == ColorSystem.NoColors);
}
