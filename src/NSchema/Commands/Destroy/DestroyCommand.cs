using System.CommandLine;
using NSchema.Operations;
using NSchema.Services;
using NSchema.State.Locks;

namespace NSchema.Commands.Destroy;

internal static class DestroyCommand
{
    public static Command Create()
    {
        var command = new Command("destroy", "Drop all managed schema objects from the target database.");

        command.Options.AddRange(DestroyOptions.All);

        command.SetAction(Run);
        return command;
    }

    private static Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken) => CommandRunner.Run(
        parseResult,
        configure: Configure,
        command: Execute,
        validator: new DestroyConfigurationValidator(),
        announceEnvironment: true,
        cancellationToken: cancellationToken
    );

    private static CliApplicationBuilder Configure(CliApplicationBuilder builder, DestroyConfiguration configuration) => builder
        .ConfigurePolicies(PolicyEnforcement.Allow, dataHazards: null)
        .ConfigureDatabase(configuration.Database)
        .ConfigureState(configuration.State, configuration.Ephemeral);

    private static async Task<int> Execute(CommandContext<DestroyConfiguration> context, CancellationToken cancellationToken)
    {
        var (app, configuration, _, _) = context;

        app.Reporter.Announce($"Destroying schema. All managed objects will be dropped from the database.");

        // Hold the state lock across the teardown plan + apply.
        var locked = await app.Locks.Acquire(new AcquireLockArguments("destroy") { SkipLock = configuration.NoLock }, cancellationToken);
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
            return await DestroyUnderLock(app, configuration, cancellationToken);
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }

    private static async Task<int> DestroyUnderLock(CliApplication app, DestroyConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!await StateRefresh.TryRefresh(app.Operations, app.Reporter, configuration.NoRefresh, cancellationToken))
        {
            return ExitCodes.Error;
        }

        var planResult = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Empty }, cancellationToken);

        // Show the plan so the operator can see what will be dropped — even on a failure the result carries it.
        var plan = planResult.Value?.Plan;
        if (plan is not null)
        {
            app.Reporter.ReportPlan(plan);
        }

        if (planResult.Diagnostics.Count > 0)
        {
            app.Reporter.ReportDiagnostics(planResult.Diagnostics);
        }

        if (planResult.IsFailure || plan is null)
        {
            return ExitCodes.Error;
        }

        // Nothing managed means nothing to drop; applying the empty plan is a clean no-op that still captures state.
        if (plan.IsEmpty)
        {
            app.Reporter.Success($"Nothing to destroy. No managed objects were found.");
            await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
            return ExitCodes.NoChanges;
        }

        // Confirmation is entirely CLI-side: the engine never prompts. Declining throws, which propagates out (the lock
        // is released by the finally in Run) and is mapped to a cancellation by Program.
        app.Reporter.Confirm(new ConfirmationRequest(
            $"NSchema will DROP managed objects via {plan.Statements.Count} statement(s). This is destructive and cannot be undone.")
        {
            Question = "Do you want to destroy these objects?",
            SkipFlag = "--auto-approve",
            AutoApprove = configuration.AutoApprove,
            Destructive = true,
        });

        var result = await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
        if (result.Diagnostics.Count > 0)
        {
            app.Reporter.ReportDiagnostics(result.Diagnostics);
        }

        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        app.Reporter.Success($"Destroy complete. {PlanNarrative.Describe(plan)}.");
        return ExitCodes.NoChanges;
    }
}
