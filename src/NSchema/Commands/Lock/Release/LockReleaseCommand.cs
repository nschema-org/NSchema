using System.CommandLine;

namespace NSchema.Commands.Lock.Release;

internal static class LockReleaseCommand
{
    internal static readonly Argument<string?> LockIdArgument = new("lock-id")
    {
        Description = "The id of the lock to release, taken from the error of the blocked operation or from lock status. " +
                      "The release is refused if it no longer matches the held lock (a safety check). Required unless " +
                      "--force is given to release whatever lock is held.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("release", "Release the state lock, even if an operation still holds it.");

        command.Arguments.Add(LockIdArgument);
        command.Options.AddRange(LockReleaseOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State),
        command: Execute,
        validator: new LockReleaseConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<LockReleaseConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        var peeked = await app.Locks.Peek(cancellationToken);
        if (peeked.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        if (peeked.Require().Held is not { } current)
        {
            app.Reporter.Announce($"No state lock is held.");
            return ExitCodes.NoChanges;
        }

        // Safe by default: when a lock id is named, it must still match the held one, so we never release a *different*
        // lock that was acquired since the caller read the id — and a redundant --force alongside an id is ignored. Only
        // when no id is given does --force take over and release whatever is held (the validator requires one or the other).
        if (configuration.LockId is { } lockId && current.Id.Value != lockId)
        {
            app.Reporter.ReportDiagnostics([
                LockDiagnostics.IdMismatch(lockId, current)
            ]);
            return ExitCodes.Error;
        }

        app.Reporter.Confirm(new ConfirmationRequest(
            $"NSchema will release the state lock, even if another operation still holds it. This can corrupt the shared state.")
        {
            Question = "Do you want to release the lock?",
            SkipFlag = "--auto-approve",
            AutoApprove = configuration.AutoApprove,
            Destructive = true,
        });

        var result = await app.Locks.Release(cancellationToken);
        if (result.ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        if (result.Require().Released is not { } released)
        {
            app.Reporter.Announce($"No state lock is held.");
            return ExitCodes.NoChanges;
        }

        app.Reporter.Success($"Released the state lock held by {released.Who} (operation '{released.Operation}', since {released.CreatedUtc:u}).");
        return ExitCodes.NoChanges;
    }
}
