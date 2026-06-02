using NSchema.Cli.Commands;

var root = RootCommand.Create();
var parseResult = root.Parse(args);

// Disable the built-in error handling so we can do our own.
var configuration = new System.CommandLine.InvocationConfiguration { EnableDefaultExceptionHandler = false };

try
{
    return await parseResult.InvokeAsync(configuration);
}
catch (OperationCanceledException)
{
    await Console.Error.WriteLineAsync("Operation cancelled.");
    return 130;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Error: {ex.Message}");
    return 1;
}
