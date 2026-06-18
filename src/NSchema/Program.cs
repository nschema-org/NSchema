using System.Text.Json;
using NSchema.Commands;
using NSchema.Configuration;
using Spectre.Console;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

var colorDisabled = CommonOptions.NoColor.GetValueOrDefault(null, parseResult, false);
var json = CommonOptions.Json.GetValueOrDefault(null, parseResult, false);
AnsiConsole.Console = ConsoleFactory.Create(Console.Out, colorDisabled);
var error = ConsoleFactory.Create(Console.Error, colorDisabled);

try
{
    return await parseResult.InvokeAsync(configuration);
}
catch (OperationCanceledException)
{
    error.MarkupLine("[yellow]Operation cancelled.[/]");
    return ExitCodes.OperationCanceled;
}
catch (Exception ex)
{
    // In --json mode keep stderr machine-readable too, so a consumer never has to parse a formatted exception.
    if (json)
    {
        // It's not gross, it's efficient.
        Console.Error.WriteLine($$"""{"type":"error","message":{{JsonSerializer.Serialize(ex.Message)}}}""");
    }
    else
    {
        error.ReportException(ex);
    }

    return ExitCodes.Error;
}
