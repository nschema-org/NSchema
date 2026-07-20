using System.Text;
using NSchema.Diff.Model;
using NSchema.Diff.Reader;
using NSchema.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that renders structured output as Markdown, for a PR comment or a CI job summary.
/// </summary>
internal sealed class MarkdownConsolePresenter : IConsolePresenter
{
    private readonly TextWriter _out;

    public MarkdownConsolePresenter() : this(Console.Out) { }

    internal MarkdownConsolePresenter(TextWriter output) => _out = output;

    public void ReportSchema(Database database) => WriteSection("Schema", Fenced(SchemaRenderer.Render(database)));

    public void ReportDiff(DatabaseDiff diff) => WriteSection("Plan", RenderDiff(diff));

    public void ReportSqlPlan(IReadOnlyList<SqlStatement> statements) => WriteSection("SQL", RenderSqlPlan(statements));

    public void ReportSavedPlan(PlanFileEnvelope envelope)
    {
        ReportDiff(envelope.Plan.Diff);
        ReportSqlPlan(envelope.Plan.Statements);
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

        var document = DiffReader.Read(diff);
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
    private static string RenderSqlPlan(IReadOnlyList<SqlStatement> statements)
    {
        if (statements.Count == 0)
        {
            return "_No statements to execute._";
        }

        var body = new StringBuilder();
        for (var i = 0; i < statements.Count; i++)
        {
            if (i > 0)
            {
                body.Append('\n');
            }

            var statement = statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            body.Append("-- [").Append(i + 1).Append('/').Append(statements.Count).Append(']').Append(marker).Append('\n');
            body.Append(statement.Sql.Value).Append('\n');
        }

        return Fenced(body.ToString(), "sql");
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
