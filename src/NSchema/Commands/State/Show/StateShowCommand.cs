using System.CommandLine;
using NSchema.Configuration;
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

    private static async ValueTask<StateShowConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<StateShowConfiguration>(result, environment, cancellationToken);
        new StateShowConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (parseResult.GetValue(FileArgument) is { } file)
        {
            return await ShowStateFile(parseResult, file, cancellationToken);
        }

        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureState(configuration.State)
            .Build();
        app.Messenger.ReportEnvironment(environment);

        app.Messenger.Announce($"Showing recorded state. The live database will not be contacted.");
        return await ShowRecordedState(app, configuration.Scope, cancellationToken);
    }

    private static async Task<int> ShowStateFile(ParseResult parseResult, string file, CancellationToken cancellationToken)
    {
        // A state file is self-contained: point a file-backed store at it and read offline — no project config needed.
        StateShowOptions.Scope.TryGetValue(parseResult, out var scope);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureState(new StateConfiguration { File = new FileStateConfiguration { Path = file } })
            .Build();

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
