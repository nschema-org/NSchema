using System.CommandLine;
using NSchema.Configuration;
using NSchema.Services.Confirmation;
using NSchema.State.Model;
using Spectre.Console;

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

    private static async ValueTask<LockReleaseConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<LockReleaseConfiguration>(result, environment, cancellationToken);
        new LockReleaseConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        var console = AnsiConsole.Console;
        app.Messenger.ReportEnvironment(environment);

        var current = await app.Locks.Peek(cancellationToken);
        if (current is null)
        {
            app.Messenger.Announce($"No state lock is held.");
            return;
        }

        // Safe by default: when a lock id is named, it must still match the held one, so we never release a *different*
        // lock that was acquired since the caller read the id — and a redundant --force alongside an id is ignored. Only
        // when no id is given does --force take over and release whatever is held (the validator requires one or the other).
        if (configuration.LockId is { } lockId && current.Id != lockId)
        {
            throw new StateLockMismatchException(lockId, current);
        }

        ConsoleConfirmationPrompt.Require(console, configuration.AutoApprove,
            "[red]NSchema will release the state lock, even if another operation still holds it. This can corrupt the shared state.[/]",
            "Do you want to release the lock? Only [green]yes[/] will be accepted:",
            "--auto-approve");

        var released = await app.Locks.Release(cancellationToken);
        if (released is null)
        {
            app.Messenger.Announce($"No state lock is held.");
            return;
        }

        app.Messenger.Success($"Released the state lock held by {released.Who} (operation '{released.Operation}', since {released.CreatedUtc:u}).");
    }
}
