using System.CommandLine;
using NSchema.State;

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

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureState(configuration.State),
        command: Execute,
        validator: new StatePullConfigurationValidator(),
        // Without a file the payload itself is the output, so narration is suppressed to keep
        // `state pull > backup.json` byte-clean.
        announceEnvironment: parseResult.GetValue(FileArgument) is not null,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<StatePullConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, _, parseResult, _) = context;
        var file = parseResult.GetValue(FileArgument);

        var result = await app.State.ReadRaw(new StateRawReadArguments(), cancellationToken);
        if (result.IsFailure)
        {
            app.Reporter.ReportDiagnostics(result.Diagnostics);
            return ExitCodes.Error;
        }

        if (result.Value.Payload is not { } payload)
        {
            app.Reporter.Warn($"No state has been recorded yet; there is nothing to pull.");
            return ExitCodes.Error;
        }

        if (file is not null)
        {
            await File.WriteAllBytesAsync(file, payload, cancellationToken);
            app.Reporter.Success($"State pulled to {file} ({payload.Length:N0} bytes).");
            return ExitCodes.NoChanges;
        }

        // The payload is the query's result: it goes to stdout verbatim (it is already JSON), the one
        // place a raw write is the honest rendering in both console modes.
        await using var stdout = Console.OpenStandardOutput();
        await stdout.WriteAsync(payload, cancellationToken);
        return ExitCodes.NoChanges;
    }
}
