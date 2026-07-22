using System.CommandLine;
using NSchema.Configuration;
using NSchema.Operations;
using NSchema.Services;
using NSchema.Services.Confirmation;
using NSchema.State.Locks;
using Spectre.Console;

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

    private static async ValueTask<DestroyConfiguration> Resolve(ParseResult result, string? environment, CancellationToken cancellationToken = default)
    {
        var config = await ConfigurationFactory.Load<DestroyConfiguration>(result, environment, cancellationToken);
        new DestroyConfigurationValidator().ValidateOrThrow(config);
        return config;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        var configuration = await Resolve(parseResult, environment, cancellationToken);

        // A teardown is fully destructive by design, so the destructive-action policy is set to Allow — the guard is
        // the confirmation prompt below, not the policy. The managed schema comes from the recorded state, so the
        // working-directory schema is not consulted.
        var builder = CliApplicationBuilder.Create(parseResult)
            .ConfigurePolicies(PolicyEnforcement.Allow, dataHazards: null)
            .ConfigureDatabase(configuration.Database)
            .ConfigureState(configuration.State, configuration.Ephemeral);

        using var app = builder.Build();
        app.Messenger.ReportEnvironment(environment);
        app.Messenger.Announce($"Destroying schema. All managed objects will be dropped from the database.");

        // Hold the state lock across the teardown plan + apply.
        var locked = await app.Locks.Acquire(new AcquireLockArguments("destroy") { SkipLock = configuration.NoLock }, cancellationToken);
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
            return await DestroyUnderLock(app, configuration, cancellationToken);
        }
        finally
        {
            await locked.Require().Release(CancellationToken.None);
        }
    }

    private static async Task<int> DestroyUnderLock(CliApplication app, DestroyConfiguration configuration, CancellationToken cancellationToken)
    {
        var planResult = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Empty }, cancellationToken);

        // Show the diff so the operator can see what will be dropped — even on a failure the result carries the plan.
        var plan = planResult.Value?.Plan;
        if (plan is not null)
        {
            app.Presenter.ReportDiff(plan.Diff);
        }

        if (planResult.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(planResult.Diagnostics);
        }

        if (planResult.IsFailure || plan is null)
        {
            return ExitCodes.Error;
        }

        // Nothing managed means nothing to drop; applying the empty plan is a clean no-op that still captures state.
        if (plan.IsEmpty)
        {
            app.Messenger.Success($"Nothing to destroy. No managed objects were found.");
            await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
            return ExitCodes.NoChanges;
        }

        app.Presenter.ReportSqlPlan(plan.Statements);

        // Confirmation is entirely CLI-side: the engine never prompts. Declining throws, which propagates out (the lock
        // is released by the finally in Run) and is mapped to a cancellation by Program.
        ConsoleConfirmationPrompt.Require(
            AnsiConsole.Console,
            configuration.AutoApprove,
            $"[red]NSchema will DROP managed objects via [yellow]{plan.Statements.Count}[/] statement(s). This is destructive and cannot be undone.[/]",
            "Do you want to destroy these objects? Only [green]yes[/] will be accepted:",
            "--auto-approve");

        var result = await app.Operations.Apply(new ApplyArguments { Plan = plan }, cancellationToken);
        if (result.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(result.Diagnostics);
        }

        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Destroy complete. {RunSummary.Describe(plan)}.");
        return ExitCodes.NoChanges;
    }
}
