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
        var configuration = NSchemaConfigurationFactory.Create(parseResult);
        using var app = ApplicationFactory.Create(configuration, requireDesiredSchema: false);
        await app.Refresh(cancellationToken);
    }
}
