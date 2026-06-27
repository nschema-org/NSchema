using Microsoft.Extensions.DependencyInjection;
using NSchema;
using NSchema.Commands;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Services;
using Spectre.Console;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

var colorDisabled = CommonOptions.NoColor.GetValueOrDefault(parseResult, false);
AnsiConsole.Console = ConsoleFactory.Create(Console.Out, colorDisabled);

try
{
    return await parseResult.InvokeAsync(configuration);
}
catch (OperationCanceledException)
{
    return Fail(ExitCodes.OperationCanceled, presenter => presenter.Warn("Operation cancelled."));
}
catch (ConfirmationDeclinedException ex)
{
    return Fail(ExitCodes.Error, presenter => presenter.Warn(ex.Message));
}
catch (Exception ex)
{
    return Fail(ExitCodes.Error, presenter => presenter.ReportException(ex));
}

// Renders a top-level outcome through the presenter — which handles text vs. --json itself — then returns the exit
// code. Builds a fresh application just for presentation: the command's own one is already gone by the time we get here.
int Fail(int exitCode, Action<IConsolePresenter> render)
{
    using var app = CliApplicationBuilder.Create(parseResult).Build();
    render(app.Services.GetRequiredService<IConsolePresenter>());
    return exitCode;
}
