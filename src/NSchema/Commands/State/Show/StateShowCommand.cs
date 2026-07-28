using System.CommandLine;
using NSchema.Configuration.State;
using NSchema.Services.Reporting;
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

        app.Messenger.Announce($"Showing recorded state. The live database will not be contacted.");
        return ShowRecordedState(app, configuration.Scope, cancellationToken);
    }

    private static async Task<int> ShowStateFile(ParseResult parseResult, string file, CancellationToken cancellationToken)
    {
        // A state file is self-contained: point a file-backed store at it and read offline — no project config needed.
        StateShowOptions.Scope.TryGetValue(parseResult, out var scope);

        var built = CliApplicationBuilder.Create(parseResult)
            .ConfigureState(new StateConfiguration { File = new FileStateConfiguration { Path = file } })
            .Build();

        if (built.ReportFailure(ReporterFactory.CreateMessenger(parseResult)))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();
        app.Messenger.Announce($"Showing state file {file}.");
        return await ShowRecordedState(app, scope, cancellationToken);
    }

    private static async Task<int> ShowRecordedState(CliApplication app, string[]? scope, CancellationToken cancellationToken)
    {
        var read = await app.State.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure)
        {
            app.Messenger.ReportDiagnostics(read.Diagnostics);
            return ExitCodes.Error;
        }

        if (read.Require().State is not { } state)
        {
            app.Messenger.Warn($"No state has been recorded yet. Run refresh to capture the schema first.");
            return ExitCodes.Error;
        }

        var planningScope = scope.ToPlanningScope();
        if (planningScope.IsFailure)
        {
            app.Messenger.ReportDiagnostics(planningScope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Presenter.ReportSchema(state.Database.ScopedTo(planningScope.Require()));
        return ExitCodes.NoChanges;
    }
}
