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

    private static async Task Refresh(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var options = NSchemaOptionsFactory.Create(parseResult);
        using var app = ApplicationFactory.Create(options);
        await app.Refresh(cancellationToken);
    }
}
