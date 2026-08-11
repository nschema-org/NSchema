using System.CommandLine;

namespace NSchema.Commands.Plan;

internal static class PlanShowCommand
{
    private static readonly Argument<string> FileArgument = new("file")
    {
        Description = "The saved plan file (from `plan --out`) to show. Renders its diff and SQL without " +
                      "contacting the database or state store.",
    };

    public static Command Create()
    {
        var command = new Command("show", "Show a saved plan file's diff and SQL.");

        command.Arguments.Add(FileArgument);

        command.SetAction(Run);
        return command;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var file = parseResult.GetRequiredValue(FileArgument);

        // A saved plan is self-contained: no project config, database, or state store is needed.
        // An unreadable .editorconfig fails the build, and requiring a value it does not carry would crash the
        // command with an internal error rather than saying what is wrong with the file.
        var built = CliApplicationBuilder.Create(parseResult).Build();
        if (built.ReportFailure(ReporterFactory.CreateReporter(parseResult)))
        {
            return ExitCodes.Error;
        }

        using var app = built.Require();

        app.Reporter.Announce($"Showing saved plan from {file}. No database or state store will be contacted.");
        var envelope = await app.PlanFile.Read(file, cancellationToken);
        if (envelope.IsFailure)
        {
            app.Reporter.ReportDiagnostics(envelope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Reporter.ReportPlan(envelope.Require().Plan);
        return ExitCodes.NoChanges;
    }
}
