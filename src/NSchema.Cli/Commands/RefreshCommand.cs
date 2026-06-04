using System.CommandLine;
using NSchema.Cli.Configuration;

namespace NSchema.Cli.Commands;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.Add(CliOptions.Global.Config);

        command.Options.Add(CliOptions.Provider.Type);
        command.Options.Add(CliOptions.Provider.ConnectionString);

        command.Options.Add(CliOptions.State.File);
        command.Options.Add(CliOptions.State.S3Bucket);
        command.Options.Add(CliOptions.State.S3Key);

        command.Options.Add(CliOptions.Migration.Scope);

        command.SetAction(Refresh);
        return command;
    }

    private static async Task Refresh(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = NSchemaConfigurationFactory.Create(parseResult);
        using var app = CliApplicationBuilder.Create(configuration)
            .ConfigureBackendState()
            .ConfigureDatabaseProvider()
            .Build();
        await app.Refresh(cancellationToken);
    }
}
