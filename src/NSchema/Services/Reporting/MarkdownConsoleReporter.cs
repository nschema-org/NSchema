using System.Text;
using NSchema.Configuration.Plugins;
using NSchema.Diff.Domain;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;
using NSchema.State.Locks;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// Outputs narration and artifacts in markdown format.
/// </summary>
internal sealed class MarkdownConsoleReporter : IConsoleReporter
{
    private readonly TextWriter _out;
    private readonly SpectreConsoleReporter _narration;
    private readonly Verbosity _verbosity;

    public MarkdownConsoleReporter(IAnsiConsole error, Verbosity verbosity) : this(Console.Out, error, verbosity) { }

    internal MarkdownConsoleReporter(TextWriter output, IAnsiConsole error, Verbosity verbosity)
    {
        _out = output;
        _narration = new SpectreConsoleReporter(error, error, verbosity);
        _verbosity = verbosity;
    }

    public void Report(MessageKind kind, string message) => _narration.Report(kind, message);

    public void Announce(ConsoleMessage message) => _narration.Announce(message);

    public void Success(ConsoleMessage message) => _narration.Success(message);

    public void Warn(ConsoleMessage message) => _narration.Warn(message);

    public void Detail(ConsoleMessage message) => _narration.Detail(message);

    public void ReportEnvironment(string? environment) => _narration.ReportEnvironment(environment);

    public void Confirm(ConfirmationRequest request) => _narration.Confirm(request);

    public void ReportException(Exception exception) => _narration.ReportException(exception);

    public void ReportDiagnostics(IReadOnlyList<Diagnostic> diagnostics) => _narration.ReportDiagnostics(diagnostics);

    public void ReportLockInfo(StateLockInfo? info) => _narration.ReportLockInfo(info);

    public void ReportScriptHashes(IReadOnlyList<ScriptHashEntry> scripts) => _narration.ReportScriptHashes(scripts);

    public void ReportProjectPlugins(IReadOnlyList<ProjectPlugin> plugins) => _narration.ReportProjectPlugins(plugins);

    public void ReportPluginDetail(ProjectPlugin plugin) => _narration.ReportPluginDetail(plugin);

    public void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins) => _narration.ReportCachedPlugins(cacheRoot, plugins);

    public void ReportOutdatedPlugins(IReadOnlyList<OutdatedPlugin> plugins) => _narration.ReportOutdatedPlugins(plugins);

    public void ReportDatabase(Database database)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(DatabaseNarrative.Summary(database));
            return;
        }

        WriteSection("Database", Fenced(DatabaseRenderer.Render(database)));
    }

    public void ReportState(DatabaseState state)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(DatabaseNarrative.Summary(state));
            return;
        }

        var database = DatabaseRenderer.Render(state.Database, state.Managed);
        WriteSection("Database", Fenced(database));
        WriteSection("Scripts", RenderScripts(state.Scripts));
    }

    public void ReportDiff(DatabaseDiff diff)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(PlanNarrative.Summary(diff));
            return;
        }

        WriteSection("Plan", RenderPlan(diff, IdentitySet.Empty));
    }

    public void ReportPlan(MigrationPlan plan)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(PlanNarrative.Summary(plan));
            return;
        }

        WriteSection("Plan", RenderPlan(plan.Diff, plan.Adopted));
        if (plan.HasStatements)
        {
            WriteSection("SQL", RenderSqlPlan(plan.Statements));
        }
    }

    public void ReportScripts(IReadOnlyList<ScriptExecution> scripts)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(ScriptLedger.Summary(scripts));
            return;
        }

        WriteSection("Scripts", RenderScripts(scripts));
    }

    // An artifact's quiet face: a plain Markdown line in place of the full sectioned rendering.
    private void Summarize(string line)
    {
        _out.Write(line);
        _out.Write("\n\n");
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
        return $"{block}{takeover}**Plan:** {PlanNarrative.Counts(document.Summary, adoptions.Count)}.";
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

            body.Append(SqlPlanNarrative.Header(i, statements.Count, statements[i])).AppendLine();
            body.Append(statements[i].Sql.Value).AppendLine();
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
