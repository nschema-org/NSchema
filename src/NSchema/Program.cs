using NSchema.Commands;
using NSchema.Configuration;
using NSchema.Services;
using Spectre.Console;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

var colorDisabled = CommonOptions.NoColor.GetValueOrDefault(parseResult, false);
var json = CommonOptions.Json.GetValueOrDefault(parseResult, false);
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
catch (ConfirmationDeclinedException ex)
{
    if (json)
    {
        JsonOutput.Write(Console.Error, new ErrorEvent(ex.Message));
    }
    else
    {
        error.MarkupLineInterpolated($"[yellow]{ex.Message}[/]");
    }

    return ExitCodes.Error;
}
catch (Exception ex)
{
    if (json)
    {
        JsonOutput.Write(Console.Error, new ErrorEvent(ex.Message));
    }
    else
    {
        error.ReportException(ex);
    }

    return ExitCodes.Error;
}
