using System.CommandLine;
using NSchema.Configuration;

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

    private static async ValueTask<LockStatusConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<LockStatusConfiguration>(result, environment, cancellationToken);
        new LockStatusConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureState(configuration.State)
            .Build();

        var info = await app.Locks.Peek(cancellationToken);

        app.Messenger.ReportEnvironment(environment);

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
