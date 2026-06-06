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

        command.Options.AddRange(ProviderOptions.All);
        command.Options.AddRange(StateOptions.All);

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
        await app.Refresh(cancellationToken);
    }
}
