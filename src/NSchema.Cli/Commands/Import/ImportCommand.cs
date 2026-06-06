using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Import;

internal static class ImportCommand
{
    public static Command Create()
    {
        var command = new Command("import", "Read the live database schema and write it as desired-schema source files.");

        command.Options.Add(CliOptions.Common.Config);

        command.Options.Add(CliOptions.Provider.Type);
        command.Options.Add(CliOptions.Provider.ConnectionString);

        command.Options.Add(CliOptions.Migration.Scope);

        command.Options.Add(CliOptions.Import.Output);
        command.Options.Add(CliOptions.Import.Format);
        command.Options.Add(CliOptions.Import.Partition);

        command.SetAction(Run);
        return command;
    }

    private static ImportConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Create(result);
        var configuration = new ImportConfiguration
        {
            Provider = config.Provider,
            ImportTarget = config.ImportTarget,
            Scope = config.Scope,
        };

        new ImportConfigurationValidator().ValidateOrThrow(configuration);
        return configuration;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = Resolve(parseResult);
        using var app = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureImportTarget(configuration.ImportTarget)
            .ConfigureImportScope(configuration.Scope)
            .Build();
        await app.Import(cancellationToken);
    }
}
