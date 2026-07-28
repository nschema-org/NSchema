using System.CommandLine;
using NSchema.Operations;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Plan;

internal static class PlanCommand
{
    public static Command Create()
    {
        var command = new Command("plan", "Compute and show the migration plan without applying it. Use --destroy to preview a teardown instead.");

        command.Options.AddRange(PlanOptions.All);
        command.Subcommands.Add(PlanShowCommand.Create());

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: Configure,
        command: Execute,
        validator: new PlanConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static CliApplicationBuilder Configure(CliApplicationBuilder builder, PlanConfiguration configuration)
    {
        // --destroy previews a teardown: fully destructive by design, so its destructive-action policy is Allow —
        // the guard is destroy's own confirmation prompt, not the policy. It plans against the recorded state, so
        // the desired schema is not read at all.
        return configuration.Destroy
            ? builder
                .ConfigurePolicies(PolicyEnforcement.Allow, configuration.DataHazardPolicy)
                .ConfigureDatabase(configuration.Database)
                .ConfigureState(configuration.State, configuration.Ephemeral)
            : builder
                .ConfigureDesiredSchema()
                .ConfigurePolicies(configuration.DestructiveActionPolicy, configuration.DataHazardPolicy)
                .ConfigureDatabase(configuration.Database)
                .ConfigureState(configuration.State, configuration.Ephemeral);
    }

    private static async Task<int> Execute(CommandContext<PlanConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Messenger.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        // The two previews are the same run against different targets: the project's desired schema, or nothing at all.
        if (configuration.Destroy)
        {
            app.Messenger.Announce($"Planning schema teardown. No changes will be applied to the database.");
        }
        else
        {
            app.Messenger.Announce($"Planning schema migration. No changes will be applied to the database.");
        }

        var result = await app.Operations.Plan(
            new PlanArguments
            {
                Scope = scope.Require(),
                OutFile = configuration.OutFile,
                Target = configuration.Destroy ? PlanTarget.Empty : PlanTarget.Project,
            },
            cancellationToken);

        return Finish(app.Presenter, app.Messenger, result, configuration.OutFile,
            configuration.Destroy ? "Planned destroy saved to" : "Plan saved to", configuration.DetailedExitCode);
    }

    // The operation returns its outcome (the plan and its diagnostics); the CLI renders them and maps the result
    // to an exit code (failure → error, otherwise the detailed code reflects whether the plan has changes).
    private static int Finish(IConsolePresenter presenter, IConsoleMessenger messenger, Result<PlanResult> result, string? outFile, string savedPrefix, bool detailed)
    {
        // A policy-blocked result still carries the complete plan, so the offending change stays visible.
        if (result.Value?.Plan is { } plan)
        {
            presenter.ReportDiff(plan.Diff);
            presenter.ReportSqlPlan(plan.Statements);
        }

        if (result.Diagnostics.Count > 0)
        {
            messenger.ReportDiagnostics(result.Diagnostics);
        }

        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        if (outFile is not null)
        {
            messenger.Success($"{savedPrefix} {outFile}. Apply it later with this file to execute exactly this plan.");
        }

        return detailed && result.Require().HasChanges ? ExitCodes.HasChanges : ExitCodes.NoChanges;
    }
}
