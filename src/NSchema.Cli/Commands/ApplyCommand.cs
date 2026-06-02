using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");
        command.Options.Add(CliOptions.AutoApprove);
        command.SetAction(Apply);
        return command;
    }

    private static async Task<int> Apply(ParseResult parseResult, CancellationToken cancellationToken)
    {
        try
        {
            var options = NSchemaOptionsFactory.Create(parseResult);
            using var app = ApplicationFactory.Create(options);
            await app.Apply(cancellationToken);
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
