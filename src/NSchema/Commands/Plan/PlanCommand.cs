using System.CommandLine;
using NSchema.Configuration;
using NSchema.Diagnostics;
using NSchema.Operations.Plan;
using NSchema.Policies;
using NSchema.Services;

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
            .ConfigureDesiredSchema(environment)
            .ConfigurePolicies(configuration.DestructiveActionPolicy)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State)
            .Build();

        app.Messenger.ReportEnvironment(environment);
        app.Messenger.Announce($"Planning schema migration. No changes will be applied to the database.");
        var result = await app.Operations.Plan(new PlanArguments { Schemas = configuration.Scope, OutFile = configuration.OutFile }, cancellationToken);
        return Finish(app.Presenter, app.Messenger, result, configuration.OutFile, "Plan saved to", configuration.DetailedExitCode);
    }

    private static async Task<int> RunDestroy(ParseResult parseResult, PlanConfiguration configuration, string? environment, CancellationToken cancellationToken)
    {
        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State);

        // The working-directory schema is only the teardown source when no state store is configured; otherwise omit
        // it so we don't glob for schema files that aren't needed.
        if (!configuration.HasStateStore)
        {
            builder.ConfigureDesiredSchema(environment);
        }

        using var app = builder.Build();
        app.Messenger.ReportEnvironment(environment);
        app.Messenger.Announce($"Planning schema teardown. No changes will be applied to the database.");
        var result = await app.Operations.Plan(new PlanArguments { OutFile = configuration.OutFile, Target = PlanTarget.Teardown }, cancellationToken);
        return Finish(app.Presenter, app.Messenger, result, configuration.OutFile, "Planned destroy saved to", configuration.DetailedExitCode);
    }

    // The operation returns its outcome (diff, generated SQL, diagnostics); the CLI renders them and maps the result
    // to an exit code (failure → error, otherwise the detailed code reflects whether the plan has changes).
    private static int Finish(IConsolePresenter presenter, IConsoleMessenger messenger, Result<PlanResult> result, string? outFile, string savedPrefix, bool detailed)
    {
        if (result.Value is { } plan)
        {
            // The diff is shown even on a policy failure, so the offending change is visible.
            if (plan.Diff is not null)
            {
                presenter.ReportDiff(plan.Diff);
            }

            // The plan (its deployment scripts) and SQL are only present for a successful plan.
            if (plan.Plan is not null)
            {
                presenter.ReportPlan(plan.Plan);
            }

            if (plan.Sql is not null)
            {
                presenter.ReportSqlPlan(plan.Sql);
            }
        }

        if (result.Diagnostics.Count > 0)
        {
            messenger.ReportDiagnostics(new PolicyDiagnostics(result.Diagnostics));
        }

        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        if (outFile is not null && result.Value.Sql is not null)
        {
            messenger.Success($"{savedPrefix} {outFile}. Apply it later with this file to execute exactly this plan.");
        }

        return detailed && result.Value.HasChanges ? ExitCodes.HasChanges : ExitCodes.NoChanges;
    }
}
