using NSchema.Commands;
using NSchema.Configuration;
using Spectre.Console;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

var colorDisabled = CommonOptions.NoColor.GetValueOrDefault(parseResult, false);
AnsiConsole.Console = ConsoleFactory.Create(Console.Out, colorDisabled);

var reporter = ReporterFactory.CreateReporter(parseResult);

try
{
    return await parseResult.InvokeAsync(configuration);
}
catch (OperationCanceledException)
{
    reporter.Report(MessageKind.Warning, "Operation cancelled.");
    return ExitCodes.OperationCanceled;
}
catch (ConfirmationDeclinedException ex)
{
    reporter.Report(MessageKind.Warning, ex.Message);
    return ExitCodes.Error;
}
catch (Exception ex)
{
    reporter.ReportException(ex);
    return ExitCodes.Error;
}
