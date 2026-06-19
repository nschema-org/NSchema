using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Drift;
using NSchema.Services;
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

    private static async ValueTask<DriftConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DriftConfiguration>(result, environment, cancellationToken);
        new DriftConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Drift(new DriftArguments { Schemas = configuration.Scope }, cancellationToken);

        return configuration.DetailedExitCode && app.Services.GetRequiredService<RunOutcome>().HasChanges
            ? ExitCodes.HasChanges
            : ExitCodes.NoChanges;
    }
}
