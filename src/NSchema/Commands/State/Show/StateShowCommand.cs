using System.CommandLine;
using NSchema.Configuration.State;
using NSchema.State;

namespace NSchema.Commands.State.Show;

internal static class StateShowCommand
{
    private static readonly Argument<string?> FileArgument = new("file")
    {
        Description = "A state file to show directly, instead of the recorded state from the configured store. " +
                      "No backend configuration is required.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("show", "Show the recorded schema state — from the configured store, or a state file given directly.");

        command.Arguments.Add(FileArgument);
        command.Options.AddRange(StateShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        // A state file given directly is self-contained, needing no project configuration, so it bypasses the preamble.
        if (parseResult.GetValue(FileArgument) is { } file)
        {
            return ShowStateFile(parseResult, file, cancellationToken);
        }
        return CommandRunner.Run(
            parseResult,
            configure: (builder, configuration) => builder.ConfigureState(configuration.State),
            command: Execute,
            validator: new StateShowConfigurationValidator(),
            announceEnvironment: true,
            cancellationToken: cancellationToken
        );
    }

    private static Task<int> Execute(CommandContext<StateShowConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        app.Reporter.Announce($"Showing recorded state. The live database will not be contacted.");
        return ShowRecordedState(app, configuration.Scope, cancellationToken);
    }

    private static async Task<int> ShowStateFile(ParseResult parseResult, string file, CancellationToken cancellationToken)
    {
        // A state file is self-contained: point a file-backed store at it and read offline — no project config needed.
        StateShowOptions.Scope.TryGetValue(parseResult, out var scope);

        var built = CliApplicationBuilder.Create(parseResult)
            .ConfigureState(new StateConfiguration { File = new FileStateConfiguration { Path = file } })
            .Build();

        if (built.ReportFailure(ReporterFactory.CreateReporter(parseResult)))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();
        app.Reporter.Announce($"Showing state file {file}.");
        return await ShowRecordedState(app, scope, cancellationToken);
    }

    private static async Task<int> ShowRecordedState(CliApplication app, string[]? scopedObjects, CancellationToken cancellationToken)
    {
        var read = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            app.Reporter.ReportDiagnostics(read.Diagnostics);
            return ExitCodes.Error;
        }

        if (read.Require().State is not { } state)
        {
            app.Reporter.Warn($"No state has been recorded yet. Run refresh to capture the schema first.");
            return ExitCodes.Error;
        }

        var scope = scopedObjects.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Reporter.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        state = state.ScopedTo(scope.Require());
        app.Reporter.ReportState(state);
        return ExitCodes.NoChanges;
    }
}
