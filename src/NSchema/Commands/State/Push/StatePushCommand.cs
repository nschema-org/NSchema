using System.CommandLine;
using NSchema.Configuration;
using NSchema.State;
using NSchema.State.Locks;

namespace NSchema.Commands.State.Push;

internal static class StatePushCommand
{
    private static readonly Argument<string> FileArgument = new("file")
    {
        Description = "The state payload to push, e.g. a pulled state file after hand-editing.",
    };

    public static Command Create()
    {
        var command = new Command("push", "Push a state payload to the configured store, replacing the recorded state. The payload is validated, then written byte-for-byte.");

        command.Arguments.Add(FileArgument);
        command.Options.AddRange(StatePushOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<StatePushConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<StatePushConfiguration>(result, environment, cancellationToken);
        new StatePushConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        var file = parseResult.GetValue(FileArgument)!;

        var payload = await File.ReadAllBytesAsync(file, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);
        app.Messenger.Announce($"Pushing state from {file}. The recorded state will be replaced.");

        var locked = await app.Locks.Acquire(new AcquireLockArguments("state push") { SkipLock = configuration.NoLock }, cancellationToken);
        if (locked.IsFailure)
        {
            app.Messenger.ReportDiagnostics(locked.Diagnostics);
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(locked.Diagnostics);
        }

        try
        {
            var result = await app.State.WriteRaw(new StateRawWriteArguments(payload), cancellationToken);
            if (result.IsFailure)
            {
                app.Messenger.ReportDiagnostics(result.Diagnostics);
                return ExitCodes.Error;
            }

            if (result.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(result.Diagnostics);
            }

            app.Messenger.Success($"State pushed ({result.Value.PayloadSize:N0} bytes).");
            return ExitCodes.NoChanges;
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }
}
