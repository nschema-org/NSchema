using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Refresh;

namespace NSchema.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<RefreshConfiguration> Resolve(ParseResult result, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<RefreshConfiguration>(result, cancellationToken);
        new RefreshConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await Resolve(parseResult, cancellationToken);
        using var app = CliApplicationBuilder.Create()
            .ConfigureBackendState(configuration.State)
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();
        await app.Refresh(new RefreshArguments(), cancellationToken);
    }
}
