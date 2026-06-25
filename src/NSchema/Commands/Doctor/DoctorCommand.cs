using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Doctor;
using Spectre.Console;

namespace NSchema.Commands.Doctor;

internal static class DoctorCommand
{
    public static Command Create()
    {
        var command = new Command("doctor", "Check that the configured database and state store are reachable and healthy.");

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<DoctorConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DoctorConfiguration>(result, environment, cancellationToken);
        new DoctorConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        await app.Doctor(new DoctorArguments(), cancellationToken);
    }
}
