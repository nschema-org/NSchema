using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Services;
using NSchema.State;
using NSchema.State.Model;
using Spectre.Console;

namespace NSchema.Commands.LockStatus;

internal static class LockStatusCommand
{
    public static Command Create()
    {
        var command = new Command("lock-status", "Show whether the state store is currently locked, and by whom.");

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
        var json = CommonOptions.Json.GetValueOrDefault(null, parseResult, false);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();

        // Read the lock directly via the public Core primitive — no live database is contacted, and Peek never
        // acquires the lock, so this can't contend with a running operation.
        var info = await app.Services.GetRequiredService<IStateLock>().Peek(cancellationToken);

        if (json)
        {
            JsonOutput.Write(Console.Out, info is null
                ? new LockStatusReport(false, null, null, null, null)
                : new LockStatusReport(true, info.Id, info.Operation, info.Who, info.CreatedUtc));
        }
        else
        {
            var console = app.Services.GetRequiredService<IAnsiConsole>();
            console.ReportEnvironment(environment);
            WriteText(console, info);
        }

        // Without --detailed-exitcode, reading the lock succeeded → 0 regardless of state. With it, a held lock is the
        // opt-in "2" signal (mirroring plan/drift), so CI can gate on it without parsing output.
        return configuration.DetailedExitCode && info is not null ? ExitCodes.HasChanges : ExitCodes.NoChanges;
    }

    private static void WriteText(IAnsiConsole console, StateLockInfo? info)
    {
        if (info is null)
        {
            console.MarkupLine("[green]✓[/] The state is not locked.");
            return;
        }

        console.MarkupLineInterpolated($"[yellow]⚠[/] The state is locked by [yellow]{info.Who}[/] (operation '{info.Operation}', since {info.CreatedUtc:u}).");
        console.MarkupLineInterpolated($"  Lock ID: [yellow]{info.Id}[/]");
        console.MarkupLineInterpolated($"  Release it, once you're sure no operation is still running, with: [green]nschema force-unlock {info.Id}[/]");
    }

    /// <summary>The <c>--json</c> shape: a single object so a script can gate on <c>locked</c> and read <c>lockId</c>.</summary>
    private sealed record LockStatusReport(bool Locked, string? LockId, string? Operation, string? Who, DateTimeOffset? Since);
}
