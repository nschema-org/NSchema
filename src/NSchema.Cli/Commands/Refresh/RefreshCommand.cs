using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Configuration.State;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.Add(CommonOptions.Config);

        command.Options.Add(ProviderOptions.Type);
        command.Options.Add(ProviderOptions.ConnectionString);

        command.Options.Add(StateOptions.File);
        command.Options.Add(StateOptions.S3Bucket);
        command.Options.Add(StateOptions.S3Key);

        command.SetAction(Run);
        return command;
    }

    private static RefreshConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Load<RefreshConfiguration>(result);
        new RefreshConfigurationValidator().ValidateOrThrow(config);
        return config;
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
