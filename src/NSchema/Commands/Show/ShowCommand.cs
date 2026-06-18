using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Show;
using Spectre.Console;

namespace NSchema.Commands.Show;

internal static class ShowCommand
{
    public static Command Create()
    {
        var command = new Command("show", "Show the schema recorded in the state store, without contacting the live database.");

        command.Options.AddRange(ShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ShowConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<ShowConfiguration>(result, environment, cancellationToken);
        new ShowConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Show(new ShowArguments { Schemas = configuration.Scope }, cancellationToken);
    }
}
