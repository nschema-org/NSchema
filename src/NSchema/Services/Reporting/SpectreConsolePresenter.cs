using NSchema.Diff.Domain;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Plan.Domain;
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
        WriteSection("Plan", RenderPlan(diff, IdentitySet.Empty));
    }

    public void ReportPlan(MigrationPlan plan)
    {
        WriteSection("Plan", RenderPlan(plan.Diff, plan.Adopted));
        if (plan.HasStatements)
        {
            WriteSection("SQL", RenderSqlPlan(plan.Statements));
        }
    }

    public void ReportSavedPlan(PlanFileEnvelope envelope) => ReportPlan(envelope.Plan);

    // A bold heading underlined to its own length
    private void WriteSection(string title, Markup body)
    {
        console.MarkupLineInterpolated($"[bold]{title}[/]");
        console.MarkupLineInterpolated($"[grey]{new string('─', title.Length)}[/]");
        console.Write(body);
        console.WriteLine();
        console.WriteLine();
    }

    private static Markup RenderPlan(DatabaseDiff diff, IdentitySet adopted)
    {
        if (diff.IsEmpty && adopted.IsEmpty)
        {
            return new Markup("No changes detected.");
        }

        var document = DiffDocument.From(diff);
        var lines = new List<string>();

        foreach (var line in document.Lines)
        {
            if (line.Change is { } change)
            {
                var (marker, colour) = DiffStyle(change);
                var text = Markup.Escape($"{new string(' ', line.Depth * 4)}{marker} {line.Text}");
                lines.Add($"[{colour}]{text}[/]");
            }
            else
            {
                lines.Add(string.Empty);
            }
        }

        // The objects the apply takes over. Nothing is done to them, so they carry no change marker of their own.
        var adoptions = PlanNarrative.AdoptedNames(adopted);
        if (adoptions.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add(Markup.Escape(PlanNarrative.AdoptionHeading(adoptions.Count)));
            lines.AddRange(adoptions.Select(name => $"[blue]{Markup.Escape($"    = {name}")}[/]"));
        }

        lines.Add(string.Empty);
        lines.Add(Markup.Escape($"Plan: {PlanNarrative.Counts(document.Summary, adoptions.Count)}"));

        return new Markup(string.Join('\n', lines));
    }

    private static (string Marker, string Colour) DiffStyle(ChangeKind change) => change switch
    {
        ChangeKind.Add => ("+", "green"),
        ChangeKind.Remove => ("-", "red"),
        ChangeKind.Modify => ("~", "yellow"),
        ChangeKind.Touched => (" ", "dim"),
        _ => ("?", "grey"),
    };

    private static Markup RenderSqlPlan(IReadOnlyList<SqlStatement> statements)
    {
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
