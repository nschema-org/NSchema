using NSchema.Cli.Commands;
using NSchema.Cli.Configuration;
using Spectre.Console;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

// Resolve color preference once (--no-color or the NO_COLOR convention) and apply it to the ambient console the
// rest of the CLI renders through, and to the stderr console used for errors and cancellation notices here.
var colorDisabled = ConsoleFactory.IsColorDisabled(parseResult);
AnsiConsole.Console = ConsoleFactory.Create(Console.Out, colorDisabled);
var error = ConsoleFactory.Create(Console.Error, colorDisabled);

try
{
    return await parseResult.InvokeAsync(configuration);
}
catch (OperationCanceledException)
{
    error.MarkupLine("[yellow]Operation cancelled.[/]");
    return 130;
}
catch (Exception ex)
{
    error.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
    return 1;
}
