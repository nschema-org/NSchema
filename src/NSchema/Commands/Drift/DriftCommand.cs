using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Services;

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
            .ConfigureDatabase(configuration.Database)
            .ConfigureState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        var result = await app.Operations.Drift(new DriftArguments { Scope = configuration.Scope.ToPlanningScope() }, cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(result.Diagnostics);
            return ExitCodes.Error;
        }

        // The operation returns the diff; the CLI renders it and the outcome line.
        var drift = result.Require();
        app.Presenter.ReportDiff(drift.Diff);
        if (drift.HasDrift)
        {
            app.Messenger.Warn($"Drift detected: {RunSummary.Describe(drift.Diff)}.");
        }
        else
        {
            app.Messenger.Success($"No drift detected.");
        }

        return configuration.DetailedExitCode && drift.HasDrift
            ? ExitCodes.HasChanges
            : ExitCodes.NoChanges;
    }
}
