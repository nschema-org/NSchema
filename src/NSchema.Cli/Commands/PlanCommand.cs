using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it.");
        command.SetAction(Plan);
        return command;
    }

    private static async Task<int> Plan(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var options = NSchemaOptionsFactory.Create(parseResult);
            using var app = ApplicationFactory.Create(options);
            await app.Plan(cancellationToken);
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
