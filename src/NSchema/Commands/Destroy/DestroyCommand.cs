using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using NSchema.Configuration;
using NSchema.Operations.Apply;
using NSchema.Operations.Plan;
using NSchema.Policies;
using NSchema.Services;
using NSchema.Services.Confirmation;
using NSchema.Sql.Model;
using NSchema.State;
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
        app.Messenger.Announce($"Destroying schema. All managed objects will be dropped from the database.");

        // Hold the state lock across the teardown plan + apply.
        var locked = await app.Locks.Acquire("destroy", configuration.NoLock, cancellationToken);
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
            return await DestroyUnderLock(app, configuration, cancellationToken);
        }
        finally
        {
            await locked.Value.Release(CancellationToken.None);
        }
    }

    private static async Task<int> DestroyUnderLock(NSchemaApplication app, DestroyConfiguration configuration, CancellationToken cancellationToken)
    {
        var planResult = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Teardown }, cancellationToken);

        // Show the diff so the operator can see what will be dropped.
        if (planResult.Value?.Diff is not null)
        {
            app.Presenter.ReportDiff(planResult.Value.Diff);
        }

        if (planResult.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. planResult.Diagnostics]));
        }

        // The trusted teardown path bypasses policies, so a failure here is a guard rather than an expected outcome.
        if (planResult.IsFailure)
        {
            return ExitCodes.Error;
        }

        var plan = planResult.Value!;

        // Destroy always runs with a provider, so the teardown plan carries SQL (empty when there is nothing managed);
        // default to an empty plan defensively so "nothing to destroy" is a clean no-op.
        var sql = plan.Sql ?? new SqlPlan([]);
        if (sql.IsEmpty)
        {
            app.Messenger.Success($"Nothing to destroy. No managed objects were found.");
            await app.Operations.Apply(new ApplyArguments { Sql = sql }, cancellationToken);
            return ExitCodes.NoChanges;
        }

        // Rendered for parity with apply/plan; a teardown plan carries no deployment scripts, so this is typically silent.
        if (plan.Plan is not null)
        {
            app.Presenter.ReportPlan(plan.Plan);
        }

        app.Presenter.ReportSqlPlan(sql);

        // Confirmation is entirely CLI-side: the engine never prompts. Declining throws, which propagates out (the lock
        // is released by the finally in Run) and is mapped to a cancellation by Program.
        ConsoleConfirmationPrompt.Require(
            app.Services.GetRequiredService<IAnsiConsole>(),
            configuration.AutoApprove,
            $"[red]NSchema will DROP managed objects via [yellow]{sql.Statements.Count}[/] statement(s). This is destructive and cannot be undone.[/]",
            "Do you want to destroy these objects? Only [green]yes[/] will be accepted:",
            "--auto-approve");

        var result = await app.Operations.Apply(new ApplyArguments { Sql = sql }, cancellationToken);
        if (result.Diagnostics.Count > 0)
        {
            app.Messenger.ReportDiagnostics(new PolicyDiagnostics([.. result.Diagnostics]));
        }

        if (result.IsFailure)
        {
            return ExitCodes.Error;
        }

        app.Messenger.Success($"Destroy complete. {RunSummary.Describe(plan.Diff!, sql)}.");
        return ExitCodes.NoChanges;
    }
}
