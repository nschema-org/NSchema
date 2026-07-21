using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.State.Locks;
using NSchema.State.Model;
using Spectre.Console;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders line-level narration and outcomes with Spectre.Console.
/// </summary>
internal sealed class SpectreConsoleMessenger : IConsoleMessenger
{
    private readonly IAnsiConsole _out;
    private readonly IAnsiConsole _error;
    private readonly Verbosity _verbosity;

    /// <param name="console">The console for informational output (typically stdout).</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsoleMessenger(IAnsiConsole console, Verbosity verbosity)
        : this(console, CreateStandardErrorConsole(console), verbosity) { }

    /// <param name="output">The console for informational output (typically stdout).</param>
    /// <param name="error">The console for errors and warnings (typically stderr).</param>
    /// <param name="verbosity">Decides which line-messages to show, per <c>--quiet</c> / <c>--verbose</c>.</param>
    public SpectreConsoleMessenger(IAnsiConsole output, IAnsiConsole error, Verbosity verbosity)
    {
        _out = output;
        _error = error;
        _verbosity = verbosity;
    }

    public void Report(MessageKind kind, string message) => WriteLine(kind, Markup.Escape(message));

    public void Announce(ConsoleMessage message) => WriteLine(MessageKind.Announcement, message.Styled);

    public void Success(ConsoleMessage message) => WriteLine(MessageKind.Success, message.Styled);

    public void Warn(ConsoleMessage message) => WriteLine(MessageKind.Warning, message.Styled);

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
            // Dimmed and italicised so verbose detail reads as secondary to the run narration.
            MessageKind.Verbose => (_out, $"[grey italic]{body}[/]"),
            _ => (_out, body),
        };

        console.MarkupLine(markup);
    }

    public void Detail(ConsoleMessage message) => _out.MarkupLine($"[grey]  {message.Styled}[/]");

    public void ReportLockInfo(StateLockInfo? info)
    {
        if (info is null)
        {
            return;
        }

        Detail($"Lock ID: {info.Id}");
        Detail($"Held by: {info.Who}");
        Detail($"Operation: {info.Operation}");
        Detail($"Since: {info.CreatedUtc:u}");

        // Surface a manual hold's lifetime, and flag it once past — but NSchema never auto-breaks an expired lock.
        if (info.ExpiresUtc is { } expires)
        {
            Detail(expires <= DateTimeOffset.UtcNow ? (ConsoleMessage)$"Expires: {expires:u} (expired)" : $"Expires: {expires:u}");
        }
    }

    public void ReportScripts(IReadOnlyList<ScriptExecution> scripts)
    {
        if (scripts.Count == 0)
        {
            _out.MarkupLine("[grey]No script executions are recorded.[/]");
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .AddColumn("Script")
            .AddColumn("Executed")
            .AddColumn("Body hash");

        foreach (var script in scripts)
        {
            table.AddRow(
                new Markup(Markup.Escape(script.Script.Value)),
                new Markup(Markup.Escape($"{script.ExecutedUtc:u}")),
                new Markup($"[grey]{Markup.Escape(script.Hash.Value)}[/]"));
        }

        _out.Write(table);
    }

    public void ReportScriptHashes(IReadOnlyList<ScriptHashEntry> scripts)
    {
        if (scripts.Count == 0)
        {
            _out.MarkupLine("[grey]No scripts are declared in this project.[/]");
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
        Detail($"Package: {plugin.PackageId}");
        Detail($"Version: {plugin.Version}");
        if (plugin.Restored)
        {
            Detail($"Restored: yes");
            Detail($"Cache path: {plugin.CachePath}");
        }
        else
        {
            Detail($"Restored: no — run 'nschema init' to restore it.");
        }
    }

    public void ReportCachedPlugins(string cacheRoot, IReadOnlyList<CachedPlugin> plugins)
    {
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
        Detail($"{plugins.Count} cached, {FormatSize(plugins.Sum(p => p.SizeBytes))} total. Remove with: nschema plugin cache remove <package> [version]");
    }

    public void ReportOutdatedPlugins(IReadOnlyList<OutdatedPlugin> plugins)
    {
        if (plugins.Count == 0)
        {
            _out.MarkupLine("[grey]No provider or backend plugins are configured for this project.[/]");
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

        var outdated = plugins.Count(plugin => plugin.Outdated);
        Detail(outdated == 0
            ? (ConsoleMessage)$"All plugins are up to date."
            : $"{outdated} outdated. Widen the range or run: nschema plugin update");
    }

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

    public void ReportException(Exception exception) =>
        _error.MarkupLineInterpolated($"[red]Error:[/] {exception.Message}");

    public void ReportDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
    {
        // Diagnostics that warrant attention (warnings, errors) belong on stderr, matching the default reporter.
        var notable = diagnostics.Any(d => d.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error);
        RenderDiagnostics(notable ? _error : _out, diagnostics);
    }

    public void ReportEnvironment(string? environment)
    {
        // Prints which environment a run is targeting, so a command run against (say) production is unmistakable.
        if (environment is null)
        {
            return;
        }

        _out.MarkupLineInterpolated($"[bold]Environment:[/] [yellow]{environment}[/]");
        _out.WriteLine();
    }

    // Renders a policy-diagnostic table.
    private static void RenderDiagnostics(IAnsiConsole console, IReadOnlyList<Diagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            console.MarkupLine("[grey]No policy diagnostics.[/]");
            console.WriteLine();
            return;
        }

        var table = new Table()
            .RoundedBorder()
            .Title("Policy diagnostics")
            .AddColumn("Severity")
            .AddColumn("Policy")
            .AddColumn("Message");

        foreach (var diagnostic in diagnostics)
        {
            table.AddRow(
                new Markup(SeverityLabel(diagnostic.Severity)),
                new Markup(Markup.Escape(diagnostic.Source)),
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

    // Mirror the output console's color decision (which already reflects --no-color / NO_COLOR) onto stderr.
    private static IAnsiConsole CreateStandardErrorConsole(IAnsiConsole output) =>
        ConsoleFactory.Create(Console.Error, output.Profile.Capabilities.ColorSystem == ColorSystem.NoColors);
}
