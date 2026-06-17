using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Import;
using Spectre.Console;

namespace NSchema.Commands.Import;

internal static class ImportCommand
{
    public static Command Create()
    {
        var command = new Command("import", "Read the live database schema and write it as desired-schema source files.");

        command.Options.AddRange(ImportOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await ConfigurationFactory.Load<ImportConfiguration>(parseResult, environment, cancellationToken);
        new ImportConfigurationValidator().ValidateOrThrow(configuration);

        using var app = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();

        app.Services.GetRequiredService<IAnsiConsole>().ReportEnvironment(environment);

        var args = new ImportArguments
        {
            Schemas = configuration.Scope,
            OutputDirectory = Path.GetFullPath(configuration.OutputDirectory ?? ".", Directory.GetCurrentDirectory())
        };

        await app.Import(args, cancellationToken);
    }
}
