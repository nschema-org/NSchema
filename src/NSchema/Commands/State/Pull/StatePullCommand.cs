using System.CommandLine;
using NSchema.Configuration;
using NSchema.Policies;
using NSchema.State.Storage;

namespace NSchema.Commands.State.Pull;

internal static class StatePullCommand
{
    private static readonly Argument<string?> FileArgument = new("file")
    {
        Description = "Write the pulled state to this file instead of standard output.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("pull", "Pull the raw recorded state payload from the configured store, for inspection, backup, or hand-editing before a push.");

        command.Arguments.Add(FileArgument);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<StatePullConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<StatePullConfiguration>(result, environment, cancellationToken);
        new StatePullConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);
        var file = parseResult.GetValue(FileArgument);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureBackendState(configuration.State)
            .Build();

        // Without a file the payload itself is the output, so narration is suppressed to keep
        // `state pull > backup.json` byte-clean.
        if (file is not null)
        {
            app.Messenger.ReportEnvironment(environment);
        }

        var result = await app.State.ReadRaw(new StateRawReadArguments(), cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
            return ExitCodes.Error;
        }

        if (result.Value.Payload is not { } payload)
        {
            app.Messenger.Warn($"No state has been recorded yet; there is nothing to pull.");
            return ExitCodes.Error;
        }

        if (file is not null)
        {
            await File.WriteAllBytesAsync(file, payload, cancellationToken);
            app.Messenger.Success($"State pulled to {file} ({payload.Length:N0} bytes).");
            return ExitCodes.NoChanges;
        }

        // The payload is the query's result: it goes to stdout verbatim (it is already JSON), the one
        // place a raw write is the honest rendering in both console modes.
        await using var stdout = Console.OpenStandardOutput();
        await stdout.WriteAsync(payload, cancellationToken);
        return ExitCodes.NoChanges;
    }
}
