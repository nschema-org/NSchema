using System.CommandLine;

namespace NSchema.Commands.Plan;

internal static class PlanShowCommand
{
    private static readonly Argument<string> FileArgument = new("file")
    {
        Description = "The saved plan file (from `plan --out`) to show. Renders its diff, plan, and SQL without " +
                      "contacting the database or state store.",
    };

    public static Command Create()
    {
        var command = new Command("show", "Show a saved plan file's diff, plan, and SQL.");

        command.Arguments.Add(FileArgument);

        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var file = parseResult.GetRequiredValue(FileArgument);

        // A saved plan is self-contained: no project config, database, or state store is needed.
        using var app = CliApplicationBuilder.Create(parseResult).Build();

        app.Messenger.Announce($"Showing saved plan from {file}. No database or state store will be contacted.");
        var envelope = await app.PlanFile.Read(file, cancellationToken);
        app.Presenter.ReportDiff(envelope.Diff);
        app.Presenter.ReportPlan(envelope.Plan);
        app.Presenter.ReportSqlPlan(envelope.Sql);
    }
}
