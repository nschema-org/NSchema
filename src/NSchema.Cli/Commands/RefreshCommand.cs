using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");
        command.SetAction(Refresh);
        return command;
    }

    private static async Task<int> Refresh(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var options = NSchemaOptionsFactory.Create(parseResult);
            using var app = ApplicationFactory.Create(options);
            await app.Refresh(cancellationToken);
            return 0;
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
    }
}
