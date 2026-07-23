using System.CommandLine;
using NSchema.Configuration;
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

    private static async ValueTask<PlanConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken)
    {
        var config = await ConfigurationFactory.Load<PlanConfiguration>(result, environment, cancellationToken);
        new PlanConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        if (configuration.Destroy)
        {
            return await RunDestroy(parseResult, configuration, environment, cancellationToken);
        }

        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigureDesiredSchema()
            .ConfigurePolicies(configuration.DestructiveActionPolicy, configuration.DataHazardPolicy)
            .ConfigureDatabase(configuration.Database)
            .ConfigureState(configuration.State, configuration.Ephemeral)
            .Build();

        app.Messenger.ReportEnvironment(environment);
        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Messenger.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.Announce($"Planning schema migration. No changes will be applied to the database.");
        var result = await app.Operations.Plan(new PlanArguments { Scope = scope.Require(), OutFile = configuration.OutFile }, cancellationToken);
        return Finish(app.Presenter, app.Messenger, result, configuration.OutFile, "Plan saved to", configuration.DetailedExitCode);
    }

    private static async Task<int> RunDestroy(ParseResult parseResult, PlanConfiguration configuration, string? environment, CancellationToken cancellationToken)
    {
        // A teardown is fully destructive by design, so the destructive-action policy is set to Allow — the
        // teardown's guard is destroy's confirmation prompt, not the policy.
        using var app = CliApplicationBuilder.Create(parseResult)
            .ConfigurePolicies(PolicyEnforcement.Allow, configuration.DataHazardPolicy)
            .ConfigureDatabase(configuration.Database)
            .ConfigureState(configuration.State, configuration.Ephemeral)
            .Build();

        app.Messenger.ReportEnvironment(environment);
        var scope = configuration.Scope.ToPlanningScope();
        if (scope.IsFailure)
        {
            app.Messenger.ReportDiagnostics(scope.Diagnostics);
            return ExitCodes.Error;
        }

        app.Messenger.Announce($"Planning schema teardown. No changes will be applied to the database.");
        var result = await app.Operations.Plan(new PlanArguments { Scope = scope.Require(), OutFile = configuration.OutFile, Target = PlanTarget.Empty }, cancellationToken);
        return Finish(app.Presenter, app.Messenger, result, configuration.OutFile, "Planned destroy saved to", configuration.DetailedExitCode);
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
