using System.CommandLine;
using NSchema.Operations;

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


    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureDatabase(configuration.Database).ConfigureState(configuration.State),
        command: Execute,
        validator: new DriftConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<DriftConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Reporter.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        var result = await app.Operations.Drift(new DriftArguments { Scope = scope.Require() }, cancellationToken);
        if (result.IsFailure)
        {
            app.Reporter.ReportDiagnostics(result.Diagnostics);
            return ExitCodes.Error;
        }

        // The operation returns the diff; the CLI renders it and the outcome line.
        var drift = result.Require();
        app.Reporter.ReportDiff(drift.Diff);
        if (drift.HasDrift)
        {
            app.Reporter.Warn($"Drift detected: {PlanNarrative.Describe(drift.Diff)}.");
        }
        else
        {
            app.Reporter.Success($"No drift detected.");
        }

        return configuration.DetailedExitCode && drift.HasDrift
            ? ExitCodes.HasChanges
            : ExitCodes.NoChanges;
    }
}
