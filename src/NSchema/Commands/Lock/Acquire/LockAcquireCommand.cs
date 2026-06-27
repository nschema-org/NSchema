using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Services;
using NSchema.State;
using NSchema.State.Model;
using Spectre.Console;

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

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();
        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();

        var stateLock = app.Services.GetRequiredService<IStateLock>();

        // Acquire and deliberately do NOT release: the lock is meant to outlive this process, to be removed later with
        // `nschema lock release`. If it is already held, Acquire throws StateLockedException with the holder's details,
        // which Program.cs presents.
        var handle = await stateLock.Acquire(new StateLockRequest(configuration.Reason, configuration.TimeToLive), cancellationToken);
        var info = handle.Info;

        presenter.Success($"Acquired the state lock (operation '{info.Operation}').");
        presenter.Detail($"Lock ID: {info.Id}");
        if (info.ExpiresUtc is { } expires)
        {
            presenter.Detail($"Expires: {expires:u}");
        }
        presenter.Detail($"The lock is held until you run: nschema lock release {info.Id}");
    }
}
