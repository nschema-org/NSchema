using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Drift;

namespace NSchema.Commands.Drift;

internal static class DriftCommand
{
    public static Command Create()
    {
        var command = new Command("drift", "Check whether the live database has drifted from the recorded state.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(DriftOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static DriftConfiguration Resolve(ParseResult result)
    {
        var config = ConfigurationFactory.Load<DriftConfiguration>(result);
        new DriftConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        using var app = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();
        await app.Drift(new DriftArguments { Schemas = configuration.Scope }, cancellationToken);
    }
}
