using NSchema.Diff.Model;
using NSchema.Diff.Reader;
using NSchema.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that presents run output with Spectre.Console.
/// Line-level messaging is the separate <see cref="IConsoleMessenger"/>; this renders only the structured output.
/// </summary>
internal sealed class SpectreConsolePresenter(IAnsiConsole console) : IConsolePresenter
{
    public void ReportSchema(Database database)
    {
        var content = SchemaRenderer.Render(database);
        var markup = new Markup(Markup.Escape(content));
        WriteSection("Schema", markup);
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        WriteSection("Plan", RenderDiff(diff));
    }

    public void ReportSqlPlan(IReadOnlyList<SqlStatement> statements)
    {
        WriteSection("SQL", RenderSqlPlan(statements));
    }

    public void ReportSavedPlan(PlanFileEnvelope envelope)
    {
        ReportDiff(envelope.Plan.Diff);
        ReportSqlPlan(envelope.Plan.Statements);
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

        var document = DiffReader.Read(diff);
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

    private static Markup RenderSqlPlan(IReadOnlyList<SqlStatement> statements)
    {
        if (statements.Count == 0)
        {
            return new Markup("- No statements to execute");
        }

        var lines = new List<string>(statements.Count * 3);

        for (var i = 0; i < statements.Count; i++)
        {
            if (i > 0)
            {
                lines.Add(string.Empty);
            }

            var statement = statements[i];
            var marker = statement.RunOutsideTransaction ? " (outside transaction)" : string.Empty;
            lines.Add($"[grey]{Markup.Escape($"-- [{i + 1}/{statements.Count}]{marker}")}[/]");
            lines.Add(Markup.Escape(statement.Sql.Value));
        }

        return new Markup(string.Join('\n', lines));
    }
}
