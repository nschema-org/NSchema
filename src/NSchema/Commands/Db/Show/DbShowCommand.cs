using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Schema;
using NSchema.Services;

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

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();
        presenter.ReportEnvironment(environment);

        presenter.Announce("Reading the live database schema.");
        var schema = await app.Services.GetRequiredService<ICurrentSchemaProvider>()
            .GetSchema(SchemaSourceMode.Online, configuration.Scope, required: true, cancellationToken);
        presenter.ReportSchema(schema);
    }
}
