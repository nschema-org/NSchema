using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.Add(CliOptions.Common.Config);

        command.Options.Add(CliOptions.Provider.Type);
        command.Options.Add(CliOptions.Provider.ConnectionString);

        command.Options.Add(CliOptions.State.File);
        command.Options.Add(CliOptions.State.S3Bucket);
        command.Options.Add(CliOptions.State.S3Key);

        command.SetAction(Run);
        return command;
    }

    private static RefreshConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Create(result);
        var configuration = new RefreshConfiguration
        {
            Provider = config.Provider,
            State = config.State,
        };

        new RefreshConfigurationValidator().ValidateOrThrow(configuration);
        return configuration;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        using var app = CliApplicationBuilder.Create()
            .ConfigureBackendState(configuration.State)
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();
        await app.Refresh(cancellationToken);
    }
}
