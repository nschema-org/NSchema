using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Refresh;

namespace NSchema.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.Add(CommonOptions.Config.Option);

        command.SetAction(Run);
        return command;
    }

    private static RefreshConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<RefreshConfiguration>(result);
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
        await app.Refresh(new RefreshArguments(), cancellationToken);
    }
}
