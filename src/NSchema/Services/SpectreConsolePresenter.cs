using System.Text;
using NSchema.Configuration;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql;
using NSchema.Sql.Model;
using NSchema.State.Model;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IConsolePresenter"/> that presents run output with Spectre.Console.
/// </summary>
internal sealed class SpectreConsolePresenter : IConsolePresenter
{
    private readonly IAnsiConsole _out;
    private readonly IAnsiConsole _error;
    private readonly IDiffRenderer _diffRenderer;
    private readonly ISchemaRenderer _schemaRenderer;
    private readonly ISqlPlanRenderer _sqlPlanRenderer;
    private readonly RunOutcome _outcome;
    private readonly OutputVerbosity _verbosity;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="schemaRenderer">The core schema renderer, reused for the recorded state shown by <c>show</c>.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    /// <param name="outcome">Records whether the reported diff had changes, for the detailed exit code.</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsolePresenter(IAnsiConsole console, IDiffRenderer diffRenderer, ISchemaRenderer schemaRenderer, ISqlPlanRenderer sqlPlanRenderer, RunOutcome outcome, OutputVerbosity verbosity)
        : this(console, CreateStandardErrorConsole(console), diffRenderer, schemaRenderer, sqlPlanRenderer, outcome, verbosity) { }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="schemaRenderer">The core schema renderer, reused for the recorded state shown by <c>show</c>.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    /// <param name="outcome">Records whether the reported diff had changes, for the detailed exit code.</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    internal SpectreConsolePresenter(IAnsiConsole output, IAnsiConsole error, IDiffRenderer diffRenderer, ISchemaRenderer schemaRenderer, ISqlPlanRenderer sqlPlanRenderer, RunOutcome outcome, OutputVerbosity verbosity)
    {
        _out = output;
        _error = error;
        _diffRenderer = diffRenderer;
        _schemaRenderer = schemaRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
        _outcome = outcome;
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
            MessageKind.Success => (_out, $"[green]:check_mark: {body}[/]"),
            MessageKind.Warning => (_error, $"[yellow]:warning: {body}[/]"),
            MessageKind.Progress => (_out, $"[grey]{body}[/]"),
            // Dimmed and italicised so verbose detail reads as secondary to the run narration.
            MessageKind.Verbose => (_out, $"[grey italic]{body}[/]"),
            _ => (_out, body),
        };

        console.MarkupLine(markup);
    }

    public void Detail(string message) => _out.MarkupLine($"[grey]  {Markup.Escape(message)}[/]");

    public void Detail(ConsoleMessage message) => _out.MarkupLine($"[grey]  {message.Styled}[/]");

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
            RenderDiagnostics(_error, violation.Errors);
        }

        _error.MarkupLineInterpolated($"[red]Error:[/] {exception.Message}");
    }

    public void ReportEnvironment(string? environment)
    {
        // Prints which environment a run is targeting, so a command run against (say) production is unmistakable.
        if (environment is null)
        {
            return;
        }

        _out.MarkupLineInterpolated($"[bold]Environment:[/] [yellow]{environment}[/]");
        _out.WriteLine();
    }

    public void ReportSchema(DatabaseSchema schema)
    {
        // A single state, not a diff — render it plainly with no marker coloring under a "Schema" section.
        var body = new Markup(Markup.Escape(_schemaRenderer.Render(schema).Trim()));
        WriteSection("Schema", body);
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        _outcome.HasChanges = !diff.IsEmpty;
        var body = ColorizeByMarker(_diffRenderer.Render(diff).Trim());
        WriteSection("Plan", body);
    }

    public void ReportPlan(MigrationPlan plan)
    {
        ReportScripts("Pre-deployment", plan.PreDeploymentScripts);
        ReportScripts("Post-deployment", plan.PostDeploymentScripts);
    }

    // Lists the script names under a section rule (the SQL itself is shown by ReportSqlPlan). An empty section is
    // skipped, matching the default reporter, so a plan with no scripts produces no output.
    private void ReportScripts(string title, IReadOnlyList<Script> scripts)
    {
        if (scripts.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();

        for (var i = 0; i < scripts.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append("  - ").Append(Markup.Escape(scripts[i].Name));
        }

        WriteSection(title, new Markup(builder.ToString()));
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        var body = DimComments(_sqlPlanRenderer.Render(plan));
        WriteSection("SQL", body);
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        // Diagnostics that warrant attention (warnings, errors) belong on stderr, matching the default reporter.
        var notable = diagnostics.Any(d => d.Severity is PolicyDiagnosticSeverity.Warning or PolicyDiagnosticSeverity.Error);
        RenderDiagnostics(notable ? _error : _out, diagnostics);
    }

    private static void RenderDiagnostics(IAnsiConsole console, IReadOnlyList<PolicyDiagnostic> diagnostics)
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

    // A bold heading underlined to its own length
    private void WriteSection(string title, Markup body)
    {
        _out.MarkupLineInterpolated($"[bold]{title}[/]");
        _out.MarkupLineInterpolated($"[grey]{new string('─', title.Length)}[/]");
        _out.Write(body);
        _out.WriteLine();
        _out.WriteLine();
    }

    // Colors each line by its leading Terraform marker. Structure, formatting, and the summary all come from the
    // core renderer — this only maps a glyph to a color, so there is no second diff renderer to keep in sync.
    private static Markup ColorizeByMarker(string text)
    {
        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length + (lines.Length * 12));

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var color = MarkerColor(line);
            var escaped = Markup.Escape(line);

            if (color is null)
            {
                builder.Append(escaped);
            }
            else
            {
                builder.Append('[').Append(color).Append(']').Append(escaped).Append("[/]");
            }

            if (i < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return new Markup(builder.ToString());
    }

    private static string? MarkerColor(string line)
    {
        foreach (var ch in line)
        {
            if (ch is ' ' or '\t')
            {
                continue;
            }

            return ch switch
            {
                '+' => "green",
                '-' => "red",
                '~' => "yellow",
                _ => null,
            };
        }

        return null;
    }

    // Dims the `-- [n/m]` statement headers so the SQL itself stands out.
    private static Markup DimComments(string text)
    {
        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length + (lines.Length * 12));

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var escaped = Markup.Escape(line);

            if (line.TrimStart().StartsWith("--", StringComparison.Ordinal))
            {
                builder.Append("[grey]").Append(escaped).Append("[/]");
            }
            else
            {
                builder.Append(escaped);
            }

            if (i < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return new Markup(builder.ToString());
    }

    // Mirror the output console's color decision (which already reflects --no-color / NO_COLOR) onto stderr.
    private static IAnsiConsole CreateStandardErrorConsole(IAnsiConsole output) =>
        ConsoleFactory.Create(Console.Error, output.Profile.Capabilities.ColorSystem == ColorSystem.NoColors);
}
