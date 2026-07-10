using System.Text;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Migrations;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that presents run output with Spectre.Console.
/// Line-level messaging is the separate <see cref="IConsoleMessenger"/>; this renders only the structured output.
/// </summary>
internal sealed class SpectreConsolePresenter(IAnsiConsole console) : IConsolePresenter
{
    public void ReportSchema(DatabaseSchema schema)
    {
        var content = SchemaRenderer.Render(schema);
        var markup = new Markup(Markup.Escape(content));
        WriteSection("Schema", markup);
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        WriteSection("Plan", RenderDiff(diff));
    }

    public void ReportPlan(MigrationPlan plan)
    {
        ReportScripts("Pre-deployment", plan.PreDeploymentScripts);
        ReportDataMigrations([.. plan.Actions.OfType<ExecuteDataMigration>()]);
        ReportScripts("Post-deployment", plan.PostDeploymentScripts);
    }

    // Lists the matched data migrations by description, mirroring the script sections. An empty section is skipped.
    private void ReportDataMigrations(IReadOnlyList<ExecuteDataMigration> migrations)
    {
        if (migrations.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < migrations.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append("  - ").Append(Markup.Escape(migrations[i].Description));
        }

        WriteSection("Data migrations", new Markup(builder.ToString()));
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
            if (scripts[i].RunCondition == RunCondition.Once)
            {
                builder.Append(" [grey](run once)[/]");
            }
        }

        WriteSection(title, new Markup(builder.ToString()));
    }

    public void ReportSqlPlan(SqlPlan plan)
    {
        WriteSection("SQL", RenderSqlPlan(plan));
    }

    public void ReportSavedPlan(PlanFileEnvelope envelope)
    {
        ReportDiff(envelope.Diff);
        ReportPlan(envelope.Plan);
        ReportSqlPlan(envelope.Sql);
    }

    // A bold heading underlined to its own length
    private void WriteSection(string title, Markup body)
    {
        console.MarkupLineInterpolated($"[bold]{title}[/]");
        console.MarkupLineInterpolated($"[grey]{new string('─', title.Length)}[/]");
        console.Write(body);
        console.WriteLine();
        console.WriteLine();
    }

    private static Markup RenderDiff(DatabaseDiff diff)
    {
        if (diff.IsEmpty)
        {
            return new Markup("No changes detected.");
        }

        var document = DiffReader.Default.Read(diff);
        var lines = new List<string>();

        foreach (var line in document.Lines)
        {
            if (line.Kind is { } kind)
            {
                var (marker, colour) = DiffStyle(kind);
                var text = Markup.Escape($"{new string(' ', line.Depth * 4)}{marker} {line.Text}");
                lines.Add($"[{colour}]{text}[/]");
            }
            else
            {
                lines.Add(string.Empty);
            }
        }

        var (added, modified, removed) = document.Summary;
        lines.Add(string.Empty);
        lines.Add(Markup.Escape($"Plan: {added} to add, {modified} to change, {removed} to destroy."));

        return new Markup(string.Join('\n', lines));
    }

    private static (string Marker, string Colour) DiffStyle(ChangeKind kind) => kind switch
    {
        ChangeKind.Add => ("+", "green"),
        ChangeKind.Remove => ("-", "red"),
        ChangeKind.Modify => ("~", "yellow"),
        _ => ("?", "grey"),
    };

    private static Markup RenderSqlPlan(SqlPlan plan)
    {
        if (plan.IsEmpty)
        {
            return new Markup("- No statements to execute");
        }

        var lines = new List<string>(plan.Statements.Count * 3);

        for (var i = 0; i < plan.Statements.Count; i++)
        {
            if (i > 0)
            {
                lines.Add(string.Empty);
            }

            var statement = plan.Statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            lines.Add($"[grey]{Markup.Escape($"-- [{i + 1}/{plan.Statements.Count}]{marker}")}[/]");
            lines.Add(Markup.Escape(statement.Sql));
        }

        return new Markup(string.Join('\n', lines));
    }
}
