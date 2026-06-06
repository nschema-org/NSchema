using NSchema.Policies;
using Spectre.Console;

namespace NSchema.Cli.Extensions;

internal static class AnsiConsoleExtensions
{
    extension(IAnsiConsole console)
    {
        public void ReportException(Exception ex)
        {
            if (ex is PolicyViolationException pve)
            {
                console.ReportDiagnostics(pve.Errors);
            }
            console.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
        }

        public void ReportDiagnostics(IReadOnlyList<PolicyDiagnostic> diagnostics)
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
                    new Markup(Markup.Escape(diagnostic.PolicyName)),
                    new Markup(Markup.Escape(diagnostic.Message)));
            }

            console.Write(table);
            console.WriteLine();
        }
    }

    private static string SeverityLabel(PolicyDiagnosticSeverity severity) => severity switch
    {
        PolicyDiagnosticSeverity.Error => "[red]error[/]",
        PolicyDiagnosticSeverity.Warning => "[yellow]warning[/]",
        _ => "[grey]info[/]",
    };
}
