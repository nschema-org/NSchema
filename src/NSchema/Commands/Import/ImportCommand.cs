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

        command.Options.AddRange(ImportOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var configuration = await ConfigurationFactory.Load<ImportConfiguration>(parseResult, cancellationToken);
        new ImportConfigurationValidator().ValidateOrThrow(configuration);

        using var app = CliApplicationBuilder.Create()
            .ConfigureDatabaseProvider(configuration.Provider)
            .Build();

        var cwd = Directory.GetCurrentDirectory();
        var target = configuration.Target;
        var args = new ImportArguments
        {
            Schemas = configuration.Scope,
            Tables = configuration.Tables,
            Partition = target.Partition,
            Format = DdlSchemaSerializer.FormatName,
            OutputFile = target.OutputFile == null ? null : Path.GetFullPath(target.OutputFile, cwd),
            OutputDirectory = target.Partition == ImportPartitionMode.None ? null : Path.GetFullPath(target.OutputDirectory ?? cwd, cwd)
        };

        await app.Import(args, cancellationToken);
    }
}
