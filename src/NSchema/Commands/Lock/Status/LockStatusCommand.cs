using System.CommandLine;

namespace NSchema.Commands.Lock.Status;

internal static class LockStatusCommand
{
    public static Command Create()
    {
        var command = new Command("status", "Show whether the state store is currently locked, and by whom.");

        command.Options.AddRange(LockStatusOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State),
        command: Execute,
        validator: new LockStatusConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<LockStatusConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, parseResult, environment) = context;

        var peeked = await app.Locks.Peek(cancellationToken);
        if (peeked.ReportFailure(app.Messenger))
        {
            return ExitCodes.Error;
        }

        var info = peeked.Require().Held;

        if (info is null)
        {
            app.Messenger.Success($"The state is not locked.");
        }
        else
        {
            app.Messenger.Warn($"The state is locked.");
        }
        app.Messenger.ReportLockInfo(info);
        if (info is not null)
        {
            app.Messenger.Detail($"Release it, once you're sure no operation is still running, with: {LockReleaseHint.Command(info.Id.Value, environment, parseResult)}");
        }

        // Without --detailed-exitcode, reading the lock succeeded → 0 regardless of state. With it, a held lock is the
        // opt-in "2" signal (mirroring plan/drift), so CI can gate on it without parsing output.
        return configuration.DetailedExitCode && info is not null ? ExitCodes.HasChanges : ExitCodes.NoChanges;
    }
}
