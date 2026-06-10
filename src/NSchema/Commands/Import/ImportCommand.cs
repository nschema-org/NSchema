using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations.Import;
using NSchema.Schema.Serialization.Ddl;

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

        var cwd = Directory.GetCurrentDirectory();
        var args = new ImportArguments
        {
            Schemas = configuration.Scope,
            Tables = configuration.Tables,
            Partition = configuration.Target.Partition,
            Format = DdlSchemaSerializer.FormatName,
            OutputFile = configuration.Target.OutputFile == null ? null : Path.GetFullPath(configuration.Target.OutputFile, cwd),
            OutputDirectory = configuration.Target.OutputDirectory == null ? null : Path.GetFullPath(configuration.Target.OutputDirectory, cwd)
        };

        await app.Import(args, cancellationToken);
    }
}
