using System.Text;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql;
using NSchema.Sql.Model;
using Spectre.Console;

namespace NSchema.Services;

/// <summary>
/// An <see cref="IConsolePresenter"/> that presents run output with Spectre.Console.
/// </summary>
internal sealed class SpectreConsolePresenter : SpectreConsoleMessenger, IConsolePresenter
{
    private readonly IDiffRenderer _diffRenderer;
    private readonly ISchemaRenderer _schemaRenderer;
    private readonly ISqlPlanRenderer _sqlPlanRenderer;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="schemaRenderer">The core schema renderer, reused for the recorded state shown by <c>state show</c>.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsolePresenter(IAnsiConsole console, IDiffRenderer diffRenderer, ISchemaRenderer schemaRenderer, ISqlPlanRenderer sqlPlanRenderer, Verbosity verbosity)
        : base(console, verbosity)
    {
        _diffRenderer = diffRenderer;
        _schemaRenderer = schemaRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
    }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="diffRenderer">The core diff renderer, reused for diff structure.</param>
    /// <param name="schemaRenderer">The core schema renderer, reused for the recorded state shown by <c>state show</c>.</param>
    /// <param name="sqlPlanRenderer">The core SQL plan renderer, reused for SQL text.</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    internal SpectreConsolePresenter(IAnsiConsole output, IAnsiConsole error, IDiffRenderer diffRenderer, ISchemaRenderer schemaRenderer, ISqlPlanRenderer sqlPlanRenderer, Verbosity verbosity)
        : base(output, error, verbosity)
    {
        _diffRenderer = diffRenderer;
        _schemaRenderer = schemaRenderer;
        _sqlPlanRenderer = sqlPlanRenderer;
    }

    public void ReportSchema(DatabaseSchema schema)
    {
        // A single state, not a diff — render it plainly with no marker coloring under a "Schema" section.
        var body = new Markup(Markup.Escape(_schemaRenderer.Render(schema).Trim()));
        WriteSection("Schema", body);
    }

    public void ReportDiff(DatabaseDiff diff)
    {
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
        RenderDiagnostics(notable ? Error : Out, diagnostics);
    }

    // A bold heading underlined to its own length
    private void WriteSection(string title, Markup body)
    {
        Out.MarkupLineInterpolated($"[bold]{title}[/]");
        Out.MarkupLineInterpolated($"[grey]{new string('─', title.Length)}[/]");
        Out.Write(body);
        Out.WriteLine();
        Out.WriteLine();
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
}
