using System.CommandLine;
using NSchema.Configuration;

namespace NSchema.Commands.Import;

internal static class ImportCommand
{
    public static Command Create()
    {
        var command = new Command("import", "Read the live database schema and write it as desired-schema source files.");

        command.Options.Add(CommonOptions.Config.Option);
        command.Options.AddRange(ImportOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = ConfigurationFactory.Load<ImportConfiguration>(parseResult);
        new ImportConfigurationValidator().ValidateOrThrow(configuration);

        using var app = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureImportTarget(configuration.Target)
            .ConfigureImportScope(configuration.Scope, configuration.Tables)
            .Build();
        await app.Import(cancellationToken);
    }
}
