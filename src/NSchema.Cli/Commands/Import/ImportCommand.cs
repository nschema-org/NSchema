using System.CommandLine;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;
using NSchema.Cli.Extensions;

namespace NSchema.Cli.Commands.Import;

internal static class ImportCommand
{
    public static Command Create()
    {
        var command = new Command("import", "Read the live database schema and write it as desired-schema source files.");

        command.Options.Add(CommonOptions.Config);
        command.Options.Add(CommonOptions.Scope);

        command.Options.Add(ProviderOptions.Type);
        command.Options.Add(ProviderOptions.ConnectionString);

        command.Options.Add(ImportTargetOptions.Output);
        command.Options.Add(ImportTargetOptions.Format);
        command.Options.Add(ImportTargetOptions.Partition);

        command.SetAction(Run);
        return command;
    }

    private static ImportConfiguration Resolve(ParseResult result)
    {
        var config = NSchemaConfigurationFactory.Load<ImportConfiguration>(result);
        new ImportConfigurationValidator().ValidateOrThrow(config);
        return config;
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
