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
        if (CheckForOverwrite(outputDirectory, configuration.Force).ReportFailure(app.Reporter))
        {
            return ExitCodes.Error;
        }

        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Reporter.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Reporter.Announce($"Importing schema from database...");

        var args = new ImportArguments
        {
            Scope = scope.Require(),
            OutputDirectory = outputDirectory
        };

        var result = await app.Operations.Import(args, cancellationToken);
        app.Reporter.ReportDiagnostics(result.Diagnostics);
        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        app.Reporter.Success($"Schema imported successfully.");
        return ExitCodes.NoChanges;
    }

    private static Result CheckForOverwrite(string outputDirectory, bool force)
    {
        if (force || !Directory.Exists(outputDirectory))
        {
            return Result.Success();
        }

        return ProjectGlobs.Enumerate(outputDirectory).Count != 0
            ? Result.From(ImportDiagnostics.OutputNotEmpty(outputDirectory))
            : Result.Success();
    }
}
