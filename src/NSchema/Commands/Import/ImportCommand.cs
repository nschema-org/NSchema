using System.CommandLine;
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

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: (builder, configuration) => builder.ConfigureDatabase(configuration.Database),
        command: Execute,
        validator: new ImportConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static async Task<int> Execute(CommandContext<ImportConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        var outputDirectory = Path.GetFullPath(configuration.OutputDirectory ?? ".", Directory.GetCurrentDirectory());
        if (CheckForOverwrite(outputDirectory, configuration.Force).ReportFailure(app.Messenger))
        {
            return ExitCodes.Error;
        }

        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Messenger.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.Announce($"Importing schema from database...");

        var args = new ImportArguments
        {
            Scope = scope.Require(),
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

    private static Result CheckForOverwrite(string outputDirectory, bool force)
    {
        if (force || !Directory.Exists(outputDirectory))
        {
            return Result.Success();
        }

        return Directory.EnumerateFiles(outputDirectory, "*.sql", SearchOption.AllDirectories).Any()
            ? Result.From(Diagnostic.Error(outputDirectory,
                $"{outputDirectory} already contains .sql files that import would overwrite. Re-run with --force to overwrite."))
            : Result.Success();
    }
}
