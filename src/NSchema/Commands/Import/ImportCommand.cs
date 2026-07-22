using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations;

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

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await ConfigurationFactory.Load<ImportConfiguration>(parseResult, environment, cancellationToken);
        new ImportConfigurationValidator().ValidateOrThrow(configuration);

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabase(configuration.Database)
            .Build();

        app.Messenger.ReportEnvironment(environment);

        var outputDirectory = Path.GetFullPath(configuration.OutputDirectory ?? ".", Directory.GetCurrentDirectory());
        GuardAgainstOverwrite(outputDirectory, configuration.Force);

        app.Messenger.Announce($"Importing schema from database...");

        var args = new ImportArguments
        {
            Scope = configuration.Scope.ToPlanningScope(),
            OutputDirectory = outputDirectory
        };

        var result = await app.Operations.Import(args, cancellationToken);
        if (result.IsFailure)
        {
            app.Messenger.ReportDiagnostics(result.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Schema imported successfully.");
        return ExitCodes.NoChanges;
    }

    private static void GuardAgainstOverwrite(string outputDirectory, bool force)
    {
        if (force || !Directory.Exists(outputDirectory))
        {
            return;
        }

        if (Directory.EnumerateFiles(outputDirectory, "*.sql", SearchOption.AllDirectories).Any())
        {
            throw new InvalidOperationException($"{outputDirectory} already contains .sql files that import would overwrite. Re-run with --force to overwrite.");
        }
    }
}
