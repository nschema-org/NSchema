using System.Text;
using NSchema.Configuration;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Sql;
using NSchema.Sql.Model;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IOperationReporter"/> that presents run output with Spectre.Console.
/// </summary>
internal sealed class SpectreOperationReporter : IOperationReporter
{
    /// <summary>
    /// The output format this reporter is registered under.
    /// </summary>
    public const string ReporterName = "fancy";

    private readonly IAnsiConsole _out;
    private readonly IAnsiConsole _error;
    private readonly IDiffRenderer _diffRenderer;
    private readonly ISqlPlanRenderer _sqlPlanRenderer;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    public SpectreOperationReporter(IAnsiConsole console, IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer)
        : this(console, CreateStandardErrorConsole(console), diffRenderer, sqlPlanRenderer) { }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    internal SpectreOperationReporter(IAnsiConsole output, IAnsiConsole error, IDiffRenderer diffRenderer, ISqlPlanRenderer sqlPlanRenderer)
    {
        _out = output;
        _error = error;
        _diffRenderer = diffRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
    }

    public string Format => ReporterName;

    public void Info(string message) => _out.MarkupLineInterpolated($"{message}");

    public void ReportException(Exception exception)
    {
        _error.WriteException(exception);
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        var body = ColorizeByMarker(_diffRenderer.Render(diff).Trim());
        WriteSection("Plan", body);
    }

    public void ReportPlan(MigrationPlan plan)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        var body = DimComments(_sqlPlanRenderer.Render(plan));
        WriteSection("SQL", body);
    }

    // A single-line rule header rather than a Panel: it gives the section visual separation without prefixing
    // every body line with a border character, so the diff/SQL underneath stays cleanly selectable and copyable.
    private void WriteSection(string title, Markup body)
    {
        _out.Write(new Rule(title).LeftJustified());
        _out.Write(body);
        _out.WriteLine();
        _out.WriteLine();
    }

    public void ReportDiagnostics(PolicyDiagnostics diagnostics)
    {
        // Diagnostics that warrant attention (warnings, errors) belong on stderr, matching the default reporter.
        var notable = diagnostics.Any(d => d.Severity is PolicyDiagnosticSeverity.Warning or PolicyDiagnosticSeverity.Error);
        var console = notable ? _error : _out;
        console.ReportDiagnostics(diagnostics);
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
