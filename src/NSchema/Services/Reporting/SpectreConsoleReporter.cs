using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Diff.Domain;
using NSchema.Diff.Rendering;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;
using NSchema.State.Locks;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace NSchema.Services.Reporting;

/// <summary>
/// The <see cref="IConsoleReporter"/> for the default text format, rendering with Spectre.Console.
/// </summary>
internal sealed class SpectreConsoleReporter : IConsoleReporter
{
    private readonly IAnsiConsole _out;
    private readonly IAnsiConsole _error;
    private readonly Verbosity _verbosity;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="verbosity">Decides which narration to show and whether artifacts summarize, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsoleReporter(IAnsiConsole console, Verbosity verbosity)
        : this(console, ConsoleFactory.CreateStandardError(console), verbosity) { }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="verbosity">Decides which narration to show and whether artifacts summarize, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsoleReporter(IAnsiConsole output, IAnsiConsole error, Verbosity verbosity)
    {
        _out = output;
        _error = error;
        _verbosity = verbosity;
    }

    public void Report(MessageKind kind, string message) => WriteLine(kind, Markup.Escape(message));

    public void Announce(ConsoleMessage message) => WriteLine(MessageKind.Announcement, message.Styled);

    public void Success(ConsoleMessage message) => WriteLine(MessageKind.Success, message.Styled);

    public void Warn(ConsoleMessage message) => WriteLine(MessageKind.Warning, message.Styled);

    public void Detail(ConsoleMessage message) => WriteLine(MessageKind.Detail, message.Styled);

    public void ReportEnvironment(string? environment)
    {
        // Which environment a run targets, so a command run against (say) production is unmistakable. Narration:
        // a quiet run has asked for outcomes only.
        if (environment is null)
        {
            return;
        }

        WriteLine(MessageKind.Announcement, $"[bold]Environment:[/] [yellow]{Markup.Escape(environment)}[/]");
        WriteLine(MessageKind.Announcement, string.Empty);
    }

    // The one narration chokepoint: every line-level message passes through this gate, so the verbosity rule is total.
    private void WriteLine(MessageKind kind, string body)
    {
        if (!_verbosity.ShouldShow(kind))
        {
            return;
        }

        var (console, markup) = kind switch
        {
            MessageKind.Success => (_out, $"[green]:check_mark: {body}[/]"),
            MessageKind.Warning => (_error, $"[yellow]:warning: {body}[/]"),
            MessageKind.Progress => (_out, $"[grey]{body}[/]"),
            MessageKind.Detail => (_out, $"[grey]  {body}[/]"),
            MessageKind.Verbose => (_out, $"[grey italic]{body}[/]"),
            _ => (_out, body),
        };

        console.MarkupLine(markup);
    }

    public void Confirm(ConfirmationRequest request)
    {
        var summary = request.Destructive ? $"[red]{request.SummaryMarkup}[/]" : request.SummaryMarkup;

        if (request.AutoApprove)
        {
            // Pre-approved: no interaction happens, so the confirmation reduces to narration.
            WriteLine(MessageKind.Announcement, summary);
            WriteLine(MessageKind.Announcement, "[grey]Auto-approve is enabled; skipping confirmation.[/]");
            return;
        }

        // Interaction is never suppressed: whatever the verbosity, the operator sees what they are approving.
        _out.MarkupLine(summary);

        // Without an interactive terminal (redirected stdin / CI / a container) there is nothing to read.
        if (!_out.Profile.Capabilities.Interactive)
        {
            throw new ConfirmationDeclinedException($"This operation needs confirmation, but there is no interactive terminal. Re-run with {request.SkipFlag} to proceed non-interactively.");
        }

        var response = _out.Prompt(new TextPrompt<string>($"{Markup.Escape(request.Question)} Only [green]yes[/] will be accepted:").AllowEmpty());

        // Any answer other than "yes" cancels.
        if (!string.Equals(response.Trim(), "yes", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfirmationDeclinedException("Cancelled by operator. No changes were made.");
        }
    }

    public void ReportException(Exception exception)
    {
        _error.MarkupLineInterpolated($"[red]Internal error ({ExceptionReport.TypeName(exception)}):[/] {ExceptionReport.Describe(exception)}");
        _error.WriteLine();
        _error.MarkupLineInterpolated($"[grey]This is a bug in NSchema. Please report it at {ExceptionReport.IssuesUrl}, including the detail below.[/]");
        _error.WriteLine();
        _error.WriteLine(ExceptionReport.Stack(exception));
    }

    public void ReportDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        // The quiet face keeps the actionable rows: Info diagnostics are narration-grade and drop out.
        var visible = _verbosity.SummarizeArtifacts
            ? diagnostics.Where(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error).ToList()
            : (IReadOnlyList<Diagnostic>)diagnostics;

        if (_verbosity.SummarizeArtifacts && visible.Count == 0)
        {
            return;
        }

        // Diagnostics that warrant attention (warnings, errors) belong on stderr, matching the warning routing.
        var notable = visible.Any(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
        RenderDiagnostics(notable ? _error : _out, visible);
    }

    public void ReportDatabase(Database database)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(DatabaseNarrative.Summary(database));
            return;
        }

        WriteSection("Database", RenderSchema(DatabaseRenderer.Render(database)));
    }

    public void ReportState(DatabaseState state)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(DatabaseNarrative.Summary(state));
            return;
        }

        WriteSection("Database", RenderSchema(DatabaseRenderer.Render(state.Database, state.Managed)));
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

    public void ReportLockInfo(StateLockInfo? info)
    {
        if (info is null)
        {
            return;
        }

        DetailLine($"Lock ID: {info.Id}");
        DetailLine($"Held by: {info.Who}");
        DetailLine($"Operation: {info.Operation}");
        DetailLine($"Since: {info.CreatedUtc:u}");

        // Surface a manual hold's lifetime, and flag it once past — but NSchema never auto-breaks an expired lock.
        if (info.ExpiresUtc is { } expires)
        {
            DetailLine(expires <= DateTimeOffset.UtcNow ? (ConsoleMessage)$"Expires: {expires:u} (expired)" : $"Expires: {expires:u}");
        }
    }

    public void ReportScriptHashes(IReadOnlyList<ScriptHashEntry> scripts)
    {
        if (scripts.Count == 0)
        {
            _out.MarkupLine("[grey]No scripts are declared in this project.[/]");
            return;
        }

        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(scripts.Count == 1 ? "1 script declared." : $"{scripts.Count} scripts declared.");
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .AddColumn("Script")
            .AddColumn("Body hash");

        foreach (var script in scripts)
        {
            table.AddRow(
                new Markup(Markup.Escape(script.Name)),
                new Markup($"[grey]{Markup.Escape(script.Hash)}[/]"));
        }

        _out.Write(table);
    }

    public void ReportProjectPlugins(IReadOnlyList<ProjectPlugin> plugins)
    {
        if (plugins.Count == 0)
        {
            _out.MarkupLine("[grey]No provider or backend plugins are configured for this project.[/]");
            return;
        }

        if (_verbosity.SummarizeArtifacts)
        {
            var missing = plugins.Count(plugin => !plugin.Restored);
            var configured = plugins.Count == 1 ? "1 plugin configured" : $"{plugins.Count} plugins configured";
            Summarize(missing == 0 ? $"{configured}, all restored." : $"{configured}, {missing} not restored.");
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .AddColumn("Role")
            .AddColumn("Plugin")
            .AddColumn("Package")
            .AddColumn("Version")
            .AddColumn("Restored");

        foreach (var plugin in plugins)
        {
            table.AddRow(
                new Markup(Markup.Escape(plugin.Role)),
                new Markup(Markup.Escape(plugin.Label.Value)),
                new Markup(Markup.Escape(plugin.PackageId.Value)),
                new Markup(Markup.Escape(plugin.Version.ToString())),
                new Markup(RestoredLabel(plugin.Restored)));
        }

        _out.Write(table);
    }

    public void ReportPluginDetail(ProjectPlugin plugin)
    {
        _out.MarkupLineInterpolated($"[bold]{plugin.Label}[/] [grey]({plugin.Role})[/]");
        DetailLine($"Package: {plugin.PackageId}");
        DetailLine($"Version: {plugin.Version}");
        if (plugin.Restored)
        {
            DetailLine($"Restored: yes");
            DetailLine($"Cache path: {plugin.CachePath}");
        }
        else
        {
            DetailLine($"Restored: no — run 'nschema init' to restore it.");
        }
    }

    public void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins)
    {
        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(plugins.Count == 0
                ? $"Plugin cache: {cacheRoot} — empty."
                : $"Plugin cache: {cacheRoot} — {plugins.Count} cached, {FormatSize(plugins.Sum(p => p.SizeBytes))} total.");
            return;
        }

        _out.MarkupLineInterpolated($"[bold]Plugin cache:[/] {cacheRoot}");

        if (plugins.Count == 0)
        {
            _out.MarkupLine("[grey]The plugin cache is empty.[/]");
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .AddColumn("Package")
            .AddColumn("Version")
            .AddColumn(new TableColumn("Size").RightAligned());

        foreach (var plugin in plugins)
        {
            table.AddRow(
                Markup.Escape(plugin.PackageId.Value),
                Markup.Escape(plugin.Version.ToString()),
                Markup.Escape(FormatSize(plugin.SizeBytes)));
        }

        _out.Write(table);
        DetailLine($"{plugins.Count} cached, {FormatSize(plugins.Sum(p => p.SizeBytes))} total. Remove with: nschema plugin cache remove <package> [version]");
    }

    public void ReportOutdatedPlugins(IReadOnlyList<OutdatedPlugin> plugins)
    {
        if (plugins.Count == 0)
        {
            _out.MarkupLine("[grey]No provider or backend plugins are configured for this project.[/]");
            return;
        }

        var outdated = plugins.Count(plugin => plugin.Outdated);

        if (_verbosity.SummarizeArtifacts)
        {
            Summarize(outdated == 0 ? "All plugins are up to date." : $"{outdated} of {plugins.Count} plugins outdated.");
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .AddColumn("Role")
            .AddColumn("Plugin")
            .AddColumn("Package")
            .AddColumn("Current")
            .AddColumn("Wanted")
            .AddColumn("Latest");

        foreach (var plugin in plugins)
        {
            table.AddRow(
                new Markup(Markup.Escape(plugin.Role)),
                new Markup(Markup.Escape(plugin.Label.Value)),
                new Markup(Markup.Escape(plugin.PackageId.Value)),
                new Markup(Markup.Escape(plugin.Current.ToString())),
                new Markup(Markup.Escape(plugin.Wanted.ToString())),
                new Markup(plugin.Outdated ? $"[yellow]{Markup.Escape(plugin.Latest.ToString())}[/]" : $"[green]{Markup.Escape(plugin.Latest.ToString())}[/]"));
        }

        _out.Write(table);

        DetailLine(outdated == 0
            ? (ConsoleMessage)$"All plugins are up to date."
            : $"{outdated} outdated. Widen the range or run: nschema plugin update");
    }

    // An artifact's quiet face: a plain line in place of the full rendering. Never gated — quiet reduces an
    // artifact, it does not remove one.
    private void Summarize(string line) => _out.MarkupLine(Markup.Escape(line));

    // An indented secondary line inside an artifact's rendering. Unlike the narration Detail, never gated: the
    // lines are part of the artifact.
    private void DetailLine(ConsoleMessage message) => _out.MarkupLine($"[grey]  {message.Styled}[/]");

    private static string RestoredLabel(bool restored) => restored ? "[green]yes[/]" : "[yellow]no[/]";

    // Compact binary size for cache listings (KiB/MiB/GiB), rounded to one decimal above a kibibyte.
    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{size:0.0} {units[unit]}";
    }

    // Renders the diagnostics table.
    private static void RenderDiagnostics(IAnsiConsole console, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            console.MarkupLine("[grey]No diagnostics.[/]");
            console.WriteLine();
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .Title("Diagnostics")
            .AddColumn("Severity")
            .AddColumn("Source")
            .AddColumn("Message");

        foreach (var diagnostic in diagnostics)
        {
            table.AddRow(
                new Markup(SeverityLabel(diagnostic.Severity)),
                new Markup(Markup.Escape(diagnostic.Source.Value)),
                new Markup(Markup.Escape(diagnostic.Message)));
        }

        console.Write(table);
        console.WriteLine();
    }

    private static string SeverityLabel(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "[red]error[/]",
        DiagnosticSeverity.Warning => "[yellow]warning[/]",
        _ => "[grey]info[/]",
    };

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
        _out.MarkupLineInterpolated($"[bold]{title}[/]");
        _out.MarkupLineInterpolated($"[grey]{new string('─', title.Length)}[/]");
        _out.Write(body);
        _out.WriteLine();
        _out.WriteLine();
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
        lines.Add(Markup.Escape($"Plan: {PlanNarrative.Counts(document.Summary, adoptions.Count)}."));

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

            lines.Add($"[grey]{Markup.Escape(SqlPlanNarrative.Header(i, statements.Count, statements[i]))}[/]");
            lines.Add(Markup.Escape(statements[i].Sql.Value));
        }

        return new Markup(string.Join('\n', lines));
    }
}
