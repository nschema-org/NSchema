using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Plan.PlanFile;
using NSchema.Services;

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
        var presenter = app.Services.GetRequiredService<IConsolePresenter>();

        presenter.Announce($"Showing saved plan from {file}. No database or state store will be contacted.");
        var envelope = await app.Services.GetRequiredService<IPlanFileWriter>().Read(file, cancellationToken);
        presenter.ReportDiff(envelope.Diff);
        presenter.ReportPlan(envelope.Plan);
        presenter.ReportSqlPlan(envelope.Sql);
    }
}
