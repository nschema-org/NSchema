using System.Text;
using NSchema.Diff.Domain;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that renders structured output as Markdown, for a PR comment or a CI job summary.
/// </summary>
internal sealed class MarkdownConsolePresenter : IConsolePresenter
{
    private readonly TextWriter _out;

    public MarkdownConsolePresenter() : this(Console.Out) { }

    internal MarkdownConsolePresenter(TextWriter output) => _out = output;

    public void ReportDatabase(Database database) => WriteSection("Database", Fenced(DatabaseRenderer.Render(database)));

    public void ReportState(DatabaseState state)
    {
        var database = DatabaseRenderer.Render(state.Database, state.Managed);
        WriteSection("Database", Fenced(database));
        WriteSection("Scripts", RenderScripts(state.Scripts));
    }

    public void ReportDiff(DatabaseDiff diff) => WriteSection("Plan", RenderPlan(diff, IdentitySet.Empty));

    public void ReportPlan(MigrationPlan plan)
    {
        WriteSection("Plan", RenderPlan(plan.Diff, plan.Adopted));
        if (plan.HasStatements)
        {
            WriteSection("SQL", RenderSqlPlan(plan.Statements));
        }
    }

    public void ReportScripts(IReadOnlyList<ScriptExecution> scripts)
    {
        WriteSection("Scripts", RenderScripts(scripts));
    }

    // The diff as a ```diff fenced block. Each line keeps its marker (+ add / - remove / ! modify) at column 0 so
    // the renderer colours it — GitHub tints ! orange — with the nesting indented after the marker. Blank spacers
    // between blocks are preserved; the summary follows.
    private static string RenderPlan(DatabaseDiff diff, IdentitySet adopted)
    {
        if (diff.IsEmpty && adopted.IsEmpty)
        {
            return "No changes detected.";
        }

        var document = DiffDocument.From(diff);
        var body = new StringBuilder();

        foreach (var line in document.Lines)
        {
            if (line.Change is { } change)
            {
                body.Append(DiffMarker(change)).Append(' ').Append(new string(' ', line.Depth * 2)).Append(line.Text).Append('\n');
            }
            else
            {
                body.Append('\n');
            }
        }

        // The objects the apply takes over. Nothing is done to them, so they list outside the diff block rather
        // than inside it wearing a marker that would misread as a change.
        var adoptions = PlanNarrative.AdoptedNames(adopted);
        var takeover = adoptions.Count == 0
            ? string.Empty
            : $"**{PlanNarrative.AdoptionHeading(adoptions.Count)}**\n\n"
                + string.Join('\n', adoptions.Select(name => $"- `{name}`")) + "\n\n";

        var block = document.Lines.Count > 0 ? $"{Fenced(body.ToString(), "diff")}\n\n" : string.Empty;
        return $"{block}{takeover}**Plan:** {PlanNarrative.Counts(document.Summary, adoptions.Count)}";
    }

    // Touched marks an element carried only by what it owns. The core's renderer emits no line for one today (a
    // touched schema contributes its contents and no header), so this arm is defensive: it keeps the two text faces
    // agreeing, and stops an unmarked element rendering as '?' if that ever changes.
    private static char DiffMarker(ChangeKind change) => change switch
    {
        ChangeKind.Add => '+',
        ChangeKind.Remove => '-',
        ChangeKind.Modify => '!',
        ChangeKind.Touched => ' ',
        _ => '?',
    };

    // The SQL as a ```sql fenced block, each statement under a numbered -- [n/m] comment that flags any running
    // outside the migration transaction.
    private static string RenderSqlPlan(IReadOnlyList<SqlStatement> statements)
    {
        var body = new StringBuilder();
        for (var i = 0; i < statements.Count; i++)
        {
            if (i > 0)
            {
                body.AppendLine();
            }

            var statement = statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            body.Append("-- [").Append(i + 1).Append('/').Append(statements.Count).Append(']').Append(marker).AppendLine();
            body.Append(statement.Sql.Value).AppendLine();
        }

        return Fenced(body.ToString(), "sql");
    }

    private static string RenderScripts(IReadOnlyList<ScriptExecution> scripts)
    {
        if (scripts.Count == 0)
        {
            return "No script executions are recorded.";
        }

        var body = new StringBuilder("| Script | Executed | Body hash |\n| --- | --- | --- |\n");
        foreach (var script in ScriptLedger.InExecutionOrder(scripts))
        {
            body.AppendLine($"| `{ScriptLedger.Name(script)}` | {ScriptLedger.Executed(script)} | `{script.Hash}` |");
        }

        return body.ToString();
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
