using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Apply;
using NSchema.Operations.Plan;
using NSchema.Plan.PlanFile;
using NSchema.Policies;
using NSchema.Services;
using NSchema.State;
using NSchema.Sql.Model;
using Spectre.Console;

namespace NSchema.Commands.Apply;

internal static class ApplyCommand
{
    public static Command Create()
    {
        var command = new Command("apply", "Compute the plan and apply it to the target database.");

        command.Options.AddRange(ApplyOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static async ValueTask<ApplyConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<ApplyConfiguration>(result, environment, cancellationToken);
        new ApplyConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureBackendState(configuration.State);

        // Replaying a saved plan runs exactly the SQL captured at plan time, so the desired schema, deployment
        // scripts, and destructive-action policy that shaped that plan are not consulted again — and the *.sql files
        // needn't even be present. A fresh apply computes the plan now, so it configures all three.
        if (configuration.PlanFile is null)
        {
            builder
                .ConfigureDesiredSchema(environment)
                .ConfigurePolicies(configuration.DestructiveActionPolicy);
        }

        using var app = builder.Build();
        app.Messenger.ReportEnvironment(environment);
        app.Messenger.Announce($"Applying schema migration. Changes will be applied to the database.");

        // Hold the state lock across the whole apply: the plan is computed and executed under the same lock.
        var locked = await app.Locks.Acquire("apply", configuration.NoLock, cancellationToken);
        if (locked.IsFailure)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. locked.Diagnostics]));
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. locked.Diagnostics]));
        }

        // Release explicitly in a finally — a lock handle is not disposable (a manual lock can outlive the process).
        try
        {
            return await ApplyUnderLock(app, configuration, cancellationToken);
        }
        finally
        {
            await locked.Value.Release(CancellationToken.None);
        }
    }

    private static async Task<int> ApplyUnderLock(NSchemaApplication app, ApplyConfiguration configuration, CancellationToken cancellationToken)
    {
        // The plan is a saved file replayed verbatim, or computed against the live database under the lock.
        PlanResult plan;
        if (configuration.PlanFile is not null)
        {
            var envelope = await app.Services.GetRequiredService<IPlanFileWriter>().Read(configuration.PlanFile, cancellationToken);
            plan = new PlanResult(envelope.Diff, envelope.Plan, envelope.Sql);
            app.Presenter.ReportDiff(envelope.Diff);
        }
        else
        {
            var planResult = await app.Operations.Plan(new PlanArguments { Schemas = configuration.Scope, Target = PlanTarget.Live }, cancellationToken);

            // Show the diff first — even on a policy error — so the offending change is visible.
            if (planResult.Value?.Diff is not null)
            {
                app.Presenter.ReportDiff(planResult.Value.Diff);
            }

            if (planResult.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. planResult.Diagnostics]));
            }

            // A blocked policy fails the plan; the diff is shown, but nothing is applied.
            if (planResult.IsFailure)
            {
                return ExitCodes.Error;
            }

            plan = planResult.Value!;
        }

        // Apply always runs with a provider, so the plan carries SQL (empty when there are no changes); default to an
        // empty plan defensively so "no changes" and "no SQL generated" collapse to the same no-op.
        var sql = plan.Sql ?? new SqlPlan([]);

        // The database already matches the desired schema. Applying still captures state (initialising the store on a
        // first run), but there is nothing to confirm or preview.
        if (sql.IsEmpty)
        {
            app.Messenger.Success($"No changes. The database already matches the desired schema.");
            await app.Operations.Apply(new ApplyArguments { Sql = sql }, cancellationToken);
            return ExitCodes.NoChanges;
        }

        // Preview the full plan — its deployment scripts then the SQL — before asking for confirmation.
        if (plan.Plan is not null)
        {
            app.Presenter.ReportPlan(plan.Plan);
        }

        app.Presenter.ReportSqlPlan(sql);

        // Confirmation is entirely CLI-side. Declining throws, which propagates out (the lock is released by the
        // finally in Run) and is mapped to a cancellation by Program.
        ConsoleConfirmationPrompt.Require(
            app.Services.GetRequiredService<IAnsiConsole>(),
            configuration.AutoApprove,
            $"NSchema will execute [yellow]{sql.Statements.Count}[/] statement(s) against the database.",
            "Do you want to apply these changes? Only [green]yes[/] will be accepted:",
            "--auto-approve");

        var result = await app.Operations.Apply(new ApplyArguments { Sql = sql }, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
            }
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Apply complete. {RunSummary.Describe(plan.Diff!, sql)}.");
        return ExitCodes.NoChanges;
    }
}
