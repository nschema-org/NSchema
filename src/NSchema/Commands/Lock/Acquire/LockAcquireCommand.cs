using System.CommandLine;
using NSchema.Configuration;
using NSchema.Policies;
using NSchema.State.Model;

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

    private static async ValueTask<LockAcquireConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<LockAcquireConfiguration>(result, environment, cancellationToken);
        new LockAcquireConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        // Deliberately do NOT release the lock:
        // the handle outlives this process, so the lock is held until `nschema lock release`.
        var result = await app.Locks.Acquire(new StateLockRequest(configuration.Reason, configuration.TimeToLive), cancellationToken: cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
            return ExitCodes.Error;
        }

        var info = result.Value.Info;
        app.Messenger.Success($"Acquired the state lock.");
        app.Messenger.ReportLockInfo(info);
        app.Messenger.Detail($"The lock is held until you run: {LockReleaseHint.Command(info.Id, environment, parseResult)}");
        return ExitCodes.NoChanges;
    }
}
