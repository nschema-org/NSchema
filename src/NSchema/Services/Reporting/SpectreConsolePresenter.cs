using NSchema.Diff.Domain;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NSchema.Services.Reporting;

/// <summary>
/// An <see cref="IConsolePresenter"/> that presents run output with Spectre.Console.
/// Line-level messaging is the separate <see cref="IConsoleMessenger"/>; this renders only the structured output.
/// </summary>
internal sealed class SpectreConsolePresenter(IAnsiConsole console) : IConsolePresenter
{
    public void ReportDatabase(Database database) => WriteSection("Database", RenderSchema(DatabaseRenderer.Render(database)));

    public void ReportState(DatabaseState state)
    {
        WriteSection("Database", RenderSchema(DatabaseRenderer.Render(state.Database, state.Managed)));
        WriteSection("Scripts", RenderScripts(state.Scripts));
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

    public void ReportScripts(IReadOnlyList<ScriptExecution> scripts) => WriteSection("Scripts", RenderScripts(scripts));

    private static IRenderable RenderScripts(IReadOnlyList<ScriptExecution> scripts)
    {
        if (scripts.Count == 0)
        {
            return new Markup("[grey]No script executions are recorded.[/]");
        }

        var table = new Table()
            .RoundedBorder()
            .AddColumn("Script")
            .AddColumn("Executed")
            .AddColumn("Body hash");

        foreach (var script in ScriptLedger.InExecutionOrder(scripts))
        {
            table.AddRow(
                new Markup(Markup.Escape(ScriptLedger.Name(script))),
                new Markup(Markup.Escape(ScriptLedger.Executed(script))),
                new Markup($"[grey]{Markup.Escape(script.Hash.Value)}[/]"));
        }

        return table;
    }

    // A bold heading underlined to its own length
    private void WriteSection(string title, IRenderable body)
    {
        console.MarkupLineInterpolated($"[bold]{title}[/]");
        console.MarkupLineInterpolated($"[grey]{new string('─', title.Length)}[/]");
        console.Write(body);
        console.WriteLine();
        console.WriteLine();
    }

    /// <summary>
    /// The rendered schema, dimming everything NSchema does not manage: the marked line, and the members under it,
    /// which are indented further and are equally none of NSchema's business. A schema with nothing marked — a live
    /// database, where management is not known — renders as plain text.
    /// </summary>
    private static Markup RenderSchema(string content)
    {
        var lines = new List<string>();

        // The indent of the unmanaged object whose block we are inside; -1 when outside one.
        var block = -1;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var indent = line.Length - line.TrimStart().Length;

            // A blank line, or one no deeper than the marked object, is outside its block.
            if (block >= 0 && (line.Length == 0 || indent <= block))
            {
                block = -1;
            }

            if (line.EndsWith(DatabaseRenderer.UnmanagedMarker, StringComparison.Ordinal))
            {
                block = indent;
            }

            lines.Add(block >= 0 ? $"[dim]{Markup.Escape(line)}[/]" : Markup.Escape(line));
        }

        return new Markup(string.Join('\n', lines));
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
