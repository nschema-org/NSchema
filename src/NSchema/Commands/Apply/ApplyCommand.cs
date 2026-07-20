using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Plan.Model;
using NSchema.Services;
using NSchema.Services.Confirmation;
using NSchema.State.Locks;
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

        // Apply re-runs the policies against whichever plan it executes, so the policy flags are configured for a
        // saved plan too. Replaying a saved plan runs exactly the SQL captured at plan time, so the desired schema
        // is not consulted again — the *.sql files needn't even be present. A fresh apply computes the plan now.
        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigureDatabaseProvider(configuration.Provider)
            .ConfigureState(configuration.State, configuration.Ephemeral)
            .ConfigurePolicies(configuration.DestructiveActionPolicy, configuration.DataHazardPolicy);

        if (configuration.PlanFile is null)
        {
            builder.ConfigureDesiredSchema();
        }

        using var app = builder.Build();
        app.Messenger.ReportEnvironment(environment);
        app.Messenger.Announce($"Applying schema migration. Changes will be applied to the database.");

        // Hold the state lock across the whole apply: the plan is computed and executed under the same lock.
        var locked = await app.Locks.Acquire(new AcquireLockArguments("apply") { SkipLock = configuration.NoLock }, cancellationToken);
        if (locked.IsFailure)
        {
            app.Messenger.ReportDiagnostics(locked.Diagnostics);
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(locked.Diagnostics);
        }

        // Release explicitly in a finally — a lock handle is not disposable (a manual lock can outlive the process).
        try
        {
            return await ApplyUnderLock(app, configuration, cancellationToken);
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }

    private static async Task<int> ApplyUnderLock(CliApplication app, ApplyConfiguration configuration, CancellationToken cancellationToken)
    {
        // The plan is a saved file replayed verbatim, or computed against the recorded state under the lock.
        MigrationPlan plan;
        if (configuration.PlanFile is not null)
        {
            var envelope = await app.PlanFile.Read(configuration.PlanFile, cancellationToken);
            if (envelope.IsFailure)
            {
                app.Messenger.ReportDiagnostics(envelope.Diagnostics);
                return ExitCodes.Error;
            }

            plan = envelope.Require().Plan;
            app.Presenter.ReportDiff(plan.Diff);
        }
        else
        {
            var planResult = await app.Operations.Plan(new PlanArguments { Scope = configuration.Scope.ToPlanningScope() }, cancellationToken);

            // Show the diff first — even on a policy error, the result carries the complete plan — so the offending
            // change is visible.
            var computed = planResult.Value?.Plan;
            if (computed is not null)
            {
                app.Presenter.ReportDiff(computed.Diff);
            }

            if (planResult.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(planResult.Diagnostics);
            }

            // A blocked policy fails the plan; the diff is shown, but nothing is applied.
            if (planResult.IsFailure || computed is null)
            {
                return ExitCodes.Error;
            }

            plan = computed;
        }

        // The database already matches the desired schema. Applying still captures state (initializing the store on a
        // first run), but there is nothing to confirm or preview.
        if (plan.IsEmpty)
        {
            app.Messenger.Success($"No changes. The database already matches the desired schema.");
            await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
            return ExitCodes.NoChanges;
        }

        // Preview the SQL before asking for confirmation (the scripts ride the diff shown above).
        app.Presenter.ReportSqlPlan(plan.Statements);

        // Confirmation is entirely CLI-side. Declining throws, which propagates out (the lock is released by the
        // finally in Run) and is mapped to a cancellation by Program.
        ConsoleConfirmationPrompt.Require(
            AnsiConsole.Console,
            configuration.AutoApprove,
            $"NSchema will execute [yellow]{plan.Statements.Count}[/] statement(s) against the database.",
            "Do you want to apply these changes? Only [green]yes[/] will be accepted:",
            "--auto-approve");

        var result = await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Diagnostics.Count > 0)
            {
                app.Messenger.ReportDiagnostics(result.Diagnostics);
            }
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Apply complete. {RunSummary.Describe(plan)}.");
        return ExitCodes.NoChanges;
    }
}
