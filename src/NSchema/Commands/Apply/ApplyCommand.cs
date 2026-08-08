using System.CommandLine;
using NSchema.Operations;
using NSchema.Plan.Domain;
using NSchema.Services;
using NSchema.State.Locks;

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

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: Configure,
        command: Execute,
        validator: new ApplyConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static CliApplicationBuilder Configure(CliApplicationBuilder builder, ApplyConfiguration configuration)
    {
        builder
            .ConfigureDatabase(configuration.Database)
            .ConfigureState(configuration.State, configuration.Ephemeral)
            .ConfigurePolicies(configuration.DestructiveActionPolicy, configuration.DataHazardPolicy);
        return configuration.PlanFile is null ? builder.ConfigureDesiredSchema() : builder;
    }

    private static async Task<int> Execute(CommandContext<ApplyConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        app.Reporter.Announce($"Applying schema migration. Changes will be applied to the database.");

        // Hold the state lock across the whole apply: the plan is computed and executed under the same lock.
        var locked = await app.Locks.Acquire(new AcquireLockArguments("apply") { SkipLock = configuration.NoLock }, cancellationToken);
        if (locked.IsFailure)
        {
            app.Reporter.ReportDiagnostics(locked.Diagnostics);
            return ExitCodes.Error;
        }

        if (locked.Diagnostics.Count > 0)
        {
            app.Reporter.ReportDiagnostics(locked.Diagnostics);
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
                app.Reporter.ReportDiagnostics(envelope.Diagnostics);
                return ExitCodes.Error;
            }

            plan = envelope.Require().Plan;
            app.Reporter.ReportPlan(plan);
        }
        else
        {
            var scope = configuration.Scope.ToPlanningScope();
            if (scope.IsFailure)
            {
                app.Reporter.ReportDiagnostics(scope.Diagnostics);
                return ExitCodes.Error;
            }

            if (!await StateRefresh.TryRefresh(app.Operations, app.Reporter, configuration.NoRefresh, cancellationToken))
            {
                return ExitCodes.Error;
            }

            var planResult = await app.Operations.Plan(new PlanArguments { Scope = scope.Require() }, cancellationToken);

            // Show the plan first — the difference and the SQL it would run. Even on a policy error, the result
            // carries the complete plan.
            var computed = planResult.Value?.Plan;
            if (computed is not null)
            {
                app.Reporter.ReportPlan(computed);
            }

            if (planResult.Diagnostics.Count > 0)
            {
                app.Reporter.ReportDiagnostics(planResult.Diagnostics);
            }

            // A blocked policy fails the plan; the diff is shown, but nothing is applied.
            if (planResult.IsFailure || computed is null)
            {
                return ExitCodes.Error;
            }

            plan = computed;
        }

        // The database already matches the desired schema, and there are no objects to adopt.
        if (plan.IsEmpty)
        {
            app.Reporter.Success($"No changes. The database already matches the desired schema.");
            await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
            return ExitCodes.NoChanges;
        }

        // Confirmation is entirely CLI-side. Declining throws, which propagates out (the lock is released by the
        // finally in Run) and is mapped to a cancellation by Program.
        app.Reporter.Confirm(Confirmation(plan, configuration.AutoApprove));

        var result = await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Diagnostics.Count > 0)
            {
                app.Reporter.ReportDiagnostics(result.Diagnostics);
            }
            return ExitCodes.Error;
        }

        app.Reporter.Success($"Apply complete. {PlanNarrative.Describe(plan)}.");
        return ExitCodes.NoChanges;
    }

    // What the operator is agreeing to. A plan can have nothing to execute and still take objects over, which is
    // the whole of what an apply would do in that case.
    private static ConfirmationRequest Confirmation(MigrationPlan plan, bool autoApprove)
    {
        var adopted = plan.Adopted.DatabaseObjects.Count + plan.Adopted.SchemaObjects.Count;
        var summary = plan.HasStatements
            ? (ConsoleMessage)$"NSchema will execute {plan.Statements.Count} statement(s) against the database."
            : $"NSchema will bring {adopted} existing object(s) under management. No SQL will be executed.";

        return new ConfirmationRequest(summary)
        {
            Question = "Do you want to apply these changes?",
            SkipFlag = "--auto-approve",
            AutoApprove = autoApprove,
        };
    }
}
