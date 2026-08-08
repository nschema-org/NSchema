using System.CommandLine;
using NSchema.Operations;
using NSchema.State.Locks;

namespace NSchema.Commands.Refresh;

internal static class RefreshCommand
{
    public static Command Create()
    {
        var command = new Command("refresh", "Read the live schema and write it to the state store.");

        command.Options.AddRange(RefreshOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State).ConfigureDatabase(configuration.Database),
        command: Execute,
        validator: new RefreshConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<RefreshConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        // Refresh writes the live schema into the store, so it takes the lock too.
        var locked = await app.Locks.Acquire(new AcquireLockArguments("refresh") { SkipLock = configuration.NoLock }, cancellationToken);
        if (locked.IsFailure)
        {
            app.Reporter.ReportDiagnostics(locked.Diagnostics);
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Reporter.ReportDiagnostics(locked.Diagnostics);
        }

        // Release explicitly in a finally — a lock handle is not disposable (a manual lock can outlive the process).
        try
        {
            var result = await app.Operations.Refresh(new RefreshArguments { Force = configuration.Force }, cancellationToken);
            if (result.IsFailure)
            {
                if (result.Diagnostics.Count > 0)
                {
                    app.Reporter.ReportDiagnostics(result.Diagnostics);
                }
                return ExitCodes.Error;
            }

            app.Reporter.Success($"State store updated successfully.");
            return ExitCodes.NoChanges;
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }
}
