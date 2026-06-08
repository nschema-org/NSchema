using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Schema;
using NSchema.Operations.Import;

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
            .Build();

        var outputPath = Path.GetFullPath(configuration.Target.OutputPath, Directory.GetCurrentDirectory());
        var args = new ImportArguments
        {
            Schemas = configuration.Scope,
            Tables = configuration.Tables,
            Partition = configuration.Target.Partition,
            Format = configuration.Target.Format.FormatName(),
            OutputDirectory = outputPath, // TODO: Separate into out-dir and out-file args?
            OutputFile = outputPath,
        };

        await app.Import(args, cancellationToken);
    }
}
