using System.CommandLine;
using NSchema.State.Locks;

namespace NSchema.Commands.Lock.Acquire;

internal static class LockAcquireCommand
{
    public static Command Create()
    {
        var command = new Command("acquire", "Take the state lock and hold it, e.g. while running out-of-band checks before a migration.");

        command.Options.AddRange(LockAcquireOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State),
        command: Execute,
        validator: new LockAcquireConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<LockAcquireConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, parseResult, environment) = context;

        // Deliberately do NOT release the lock:
        // the handle outlives this process, so the lock is held until `nschema lock release`.
        var result = await app.Locks.Acquire(new AcquireLockArguments(configuration.Reason) { TimeToLive = configuration.TimeToLive }, cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(result.Diagnostics);
            return ExitCodes.Error;
        }

        var info = result.Require().Info;
        app.Messenger.Success($"Acquired the state lock.");
        app.Messenger.ReportLockInfo(info);
        app.Messenger.Detail($"The lock is held until you run: {LockReleaseHint.Command(info.Id.Value, environment, parseResult)}");
        return ExitCodes.NoChanges;
    }
}
