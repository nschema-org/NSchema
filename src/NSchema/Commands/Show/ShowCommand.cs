using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Show;

namespace NSchema.Commands.Show;

internal static class ShowCommand
{
    public static Command Create()
    {
        var command = new Command("show", "Show the schema recorded in the state store, without contacting the live database.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(ShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static ShowConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<ShowConfiguration>(result);
        new ShowConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        using var app = CliApplicationBuilder.Create()
            .ConfigureBackendState(configuration.State)
            .Build();
        await app.Show(new ShowArguments { Schemas = configuration.Scope }, cancellationToken);
    }
}
