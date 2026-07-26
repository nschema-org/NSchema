using System.CommandLine;
using NSchema.Configuration;

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

    private static async ValueTask<DatabaseShowConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<DatabaseShowConfiguration>(result, environment, cancellationToken);
        new DatabaseShowConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabase(configuration.Provider)
            .Build();
        app.Messenger.ReportEnvironment(environment);

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
