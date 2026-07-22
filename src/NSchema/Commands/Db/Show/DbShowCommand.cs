using System.CommandLine;
using NSchema.Configuration;

namespace NSchema.Commands.Db.Show;

internal static class DbShowCommand
{
    public static Command Create()
    {
        var command = new Command("show", "Show the live database schema, read directly from the database via the provider.");

        command.Options.AddRange(DbShowOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<DbShowConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<DbShowConfiguration>(result, environment, cancellationToken);
        new DbShowConfigurationValidator().ValidateOrThrow(config);
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

        app.Messenger.Announce($"Reading the live database schema.");
        var database = await app.Database.GetDatabase(configuration.Scope.ToPlanningScope(), cancellationToken);
        if (database.IsFailure)
        {
            app.Messenger.ReportDiagnostics(database.Diagnostics);
            return ExitCodes.Error;
        }

        app.Presenter.ReportSchema(database.Require());
        return ExitCodes.NoChanges;
    }
}
