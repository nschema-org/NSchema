using System.CommandLine;

namespace NSchema.Commands.Database.Show;

internal static class DatabaseShowCommand
{
    public static Command Create()
    {
        var command = new Command("show", "Show the live database schema, read directly from the database via the provider.");

        command.Options.AddRange(DatabaseShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureDatabase(configuration.Provider),
        command: Execute,
        validator: new DatabaseShowConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<DatabaseShowConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Messenger.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.Announce($"Reading the live database schema.");
        var database = await app.Database.GetDatabase(scope.Require(), cancellationToken);
        if (database.IsFailure)
        {
            app.Messenger.ReportDiagnostics(database.Diagnostics);
            return ExitCodes.Error;
        }

        app.Presenter.ReportSchema(database.Require());
        return ExitCodes.NoChanges;
    }
}
