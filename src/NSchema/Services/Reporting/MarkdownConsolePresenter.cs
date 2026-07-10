using System.Text;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Migrations;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that renders structured output as Markdown, for a PR comment or a CI job summary.
/// </summary>
internal sealed class MarkdownConsolePresenter : IConsolePresenter
{
    private readonly TextWriter _out;

    public MarkdownConsolePresenter() : this(Console.Out) { }

    internal MarkdownConsolePresenter(TextWriter output) => _out = output;

    public void ReportSchema(DatabaseSchema schema) => WriteSection("Schema", Fenced(SchemaRenderer.Render(schema)));

    public void ReportDiff(DatabaseDiff diff) => WriteSection("Plan", RenderDiff(diff));

    public void ReportSqlPlan(SqlPlan plan) => WriteSection("SQL", RenderSqlPlan(plan));

    public void ReportPlan(MigrationPlan plan)
    {
        WriteScripts("Pre-deployment", plan.PreDeploymentScripts);
        WriteDataMigrations(plan.Actions.OfType<ExecuteDataMigration>().ToList());
        WriteScripts("Post-deployment", plan.PostDeploymentScripts);
    }

    private void WriteDataMigrations(IReadOnlyList<ExecuteDataMigration> migrations)
    {
        if (migrations.Count == 0)
        {
            return;
        }

        var body = new StringBuilder();
        foreach (var migration in migrations)
        {
            body.Append("- `").Append(migration.Description).Append("`\n");
        }

        WriteSection("Data migrations", body.ToString());
    }

    public void ReportSavedPlan(PlanFileEnvelope envelope)
    {
        ReportDiff(envelope.Diff);
        ReportPlan(envelope.Plan);
        ReportSqlPlan(envelope.Sql);
    }

    // The diff as a ```diff fenced block. Each line keeps its marker (+ add / - remove / ! modify) at column 0 so
    // the renderer colours it — GitHub tints ! orange — with the nesting indented after the marker. Blank spacers
    // between blocks are preserved; the summary follows.
    private static string RenderDiff(DatabaseDiff diff)
    {
        if (diff.IsEmpty)
        {
            return "No changes detected.";
        }

        var document = DiffReader.Default.Read(diff);
        var body = new StringBuilder();

        foreach (var line in document.Lines)
        {
            if (line.Kind is { } kind)
            {
                body.Append(DiffMarker(kind)).Append(' ').Append(new string(' ', line.Depth * 2)).Append(line.Text).Append('\n');
            }
            else
            {
                body.Append('\n');
            }
        }

        var (added, modified, removed) = document.Summary;
        return $"{Fenced(body.ToString(), "diff")}\n\n**Plan:** {added} to add, {modified} to change, {removed} to destroy.";
    }

    private static char DiffMarker(ChangeKind kind) => kind switch
    {
        ChangeKind.Add => '+',
        ChangeKind.Remove => '-',
        ChangeKind.Modify => '!',
        _ => '?',
    };

    // The SQL as a ```sql fenced block, each statement under a numbered -- [n/m] comment that flags any running
    // outside the migration transaction.
    private static string RenderSqlPlan(SqlPlan plan)
    {
        if (plan.IsEmpty)
        {
            return "_No statements to execute._";
        }

        var body = new StringBuilder();
        for (var i = 0; i < plan.Statements.Count; i++)
        {
            if (i > 0)
            {
                body.Append('\n');
            }

            var statement = plan.Statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            body.Append("-- [").Append(i + 1).Append('/').Append(plan.Statements.Count).Append(']').Append(marker).Append('\n');
            body.Append(statement.Sql).Append('\n');
        }

        return Fenced(body.ToString(), "sql");
    }

    private void WriteScripts(string title, IReadOnlyList<Script> scripts)
    {
        if (scripts.Count == 0)
        {
            return;
        }

        var body = new StringBuilder();
        foreach (var script in scripts)
        {
            body.Append("- `").Append(script.Name).Append('`');
            if (script.RunCondition == RunCondition.Once)
            {
                body.Append(" *(run once)*");
            }
            body.Append('\n');
        }

        WriteSection(title, body.ToString());
    }

    private static string Fenced(string content, string language = "") =>
        $"```{language}\n{content.TrimEnd('\n')}\n```";

    private void WriteSection(string title, string body)
    {
        _out.Write("## ");
        _out.Write(title);
        _out.Write("\n\n");
        _out.Write(body.TrimEnd('\n'));
        _out.Write("\n\n");
    }
}
