using System.Text;
using NSchema.Cli.Configuration;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Sql;
using NSchema.Sql.Model;
using Spectre.Console;

namespace NSchema.Cli.Services;

/// <summary>
/// An <see cref="IMigrationReporter"/> that presents run output with Spectre.Console.
/// </summary>
internal sealed class SpectreMigrationReporter : IMigrationReporter
{
    /// <summary>
    /// The output format this reporter is registered under.
    /// </summary>
    public const string FormatName = "fancy";

    private readonly IAnsiConsole _out;
    private readonly IAnsiConsole _error;
    private readonly IDiffRenderer _diffRenderer;
    private readonly ISqlPlanRenderer _sqlPlanRenderer;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    public SpectreMigrationReporter(IAnsiConsole console, IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer)
        : this(console, CreateStandardErrorConsole(console), diffRenderer, sqlPlanRenderer) { }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    internal SpectreMigrationReporter(IAnsiConsole output, IAnsiConsole error, IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer)
    {
        _out = output;
        _error = error;
        _diffRenderer = diffRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
    }

    public string Format => FormatName;

    public void Info(string message) => _out.MarkupLineInterpolated($"{message}");

    public void Error(string message) => _error.MarkupLineInterpolated($"[red]{message}[/]");

    public void ReportDiff(MigrationDiff diff)
    {
        var body = ColorizeByMarker(_diffRenderer.Render(diff));
        _out.Write(new Panel(body).Header(" Plan ").RoundedBorder());
        _out.WriteLine();
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        var body = DimComments(_sqlPlanRenderer.Render(plan));
        _out.Write(new Panel(body).Header(" SQL ").RoundedBorder());
        _out.WriteLine();
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        // Diagnostics that warrant attention (warnings, errors) belong on stderr, matching the default reporter.
        var notable = diagnostics.Any(d => d.Severity is PolicyDiagnosticSeverity.Warning or PolicyDiagnosticSeverity.Error);
        var console = notable ? _error : _out;

        if (diagnostics.Count == 0)
        {
            _out.MarkupLine("[grey]No policy diagnostics.[/]");
            _out.WriteLine();
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
