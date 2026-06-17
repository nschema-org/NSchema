using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Drift;
using Spectre.Console;

namespace NSchema.Commands.Drift;

internal static class DriftCommand
{
    public static Command Create()
    {
        var command = new Command("drift", "Check whether the live database has drifted from the recorded state.");

        command.Options.AddRange(DriftOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<DriftConfiguration> Resolve(ParseResult result, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DriftConfiguration>(result, cancellationToken);
        new DriftConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await Resolve(parseResult, cancellationToken);
        using var app = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(configuration.Environment);
        await app.Drift(new DriftArguments { Schemas = configuration.Scope }, cancellationToken);
    }
}
