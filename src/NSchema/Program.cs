using NSchema.Commands;
using NSchema.Configuration;
using Spectre.Console;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

var colorDisabled = CommonOptions.NoColor.GetValueOrDefault(parseResult, false);
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
    error.ReportException(ex);
    return 1;
}
