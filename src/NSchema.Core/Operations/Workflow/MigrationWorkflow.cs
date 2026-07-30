using NSchema.Deployment;
using NSchema.Diff.Domain.Services;
using NSchema.Model;
using NSchema.Operations.Progress;
using NSchema.Plan.Domain;
using NSchema.Plan.Domain.Services;
using NSchema.Project;
using NSchema.Project.Domain.Directives;
using NSchema.State;
using NSchema.State.Domain;

namespace NSchema.Operations.Workflow;

internal sealed class MigrationWorkflow(
    IMigrationPlanner planner,
    IProgress<OperationProgress> progress,
    IDatabaseProvider databaseProvider,
    IProjectProvider projectProvider,
    IDatabaseStateManager stateManager
) : IMigrationWorkflow
{
    public async Task<Result> Validate(CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Loading project..."));
        var diagnostics = new DiagnosticCollector();
        if (!diagnostics.TryTake(await projectProvider.GetProject(PlanningScope.All, cancellationToken), out var project))
        {
            return diagnostics.ToResult();
        }
        ReportProjectDetail(project);

        progress.Report(OperationProgress.Step("Validating project..."));

        // The findings — including any non-error advisories and findings raised while reading the DDL — are
        // returned as data for the caller to render.
        diagnostics.AddRange(planner.Validate(project));
        return diagnostics.ToResult();
    }

    public async Task<Result<MigrationPlan>> ComputePlan(PlanTarget target, PlanningScope scope, CancellationToken cancellationToken = default)
    {
        // The diff is computed against CurrentState — the schema plus the run-once ledger — so planning without
        // a store would plan against knowingly incomplete current state.
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<MigrationPlan>(WorkflowDiagnostics.StoreRequiredForPlanning);
        }

        var diagnostics = new DiagnosticCollector();
        if (!diagnostics.TryTake(await ResolveTarget(target, scope, cancellationToken), out var project))
        {
            return diagnostics.ToResult<MigrationPlan>(null);
        }

        // Fetch the current state.
        if (!diagnostics.TryTake(await stateManager.Read(new StateReadArguments(), cancellationToken), out var read))
        {
            return diagnostics.ToResult<MigrationPlan>(null);
        }
        // Coalesce to empty if it's a first-time run.
        var state = read.State ?? DatabaseState.Empty;

        // An unscoped run auto-scopes to a union of the project and the existing state.
        var scopeInEffect = scope.IsUnscoped
            ? PlanningScope.To(project.AddressedSchemas.Concat(state.Managed.Schemas))
            : scope;

        progress.Report(OperationProgress.Step($"Migration will be scoped to: {Describe(scopeInEffect)}"));

        // The current side is not narrowed here: scope applies to the difference, once computed. Filtering it
        // away first would hide the out-of-scope objects a scoped run may still disturb.
        // TODO: Move progress reporting down into the planner where the scoping is done?
        progress.Report(OperationProgress.Detail($"Recorded state: {StatusHelpers.Describe(state.Database.ScopedTo(scopeInEffect))}."));

        progress.Report(OperationProgress.Step("Computing migration plan..."));

        var current = new CurrentState(state.Database, state.Scripts, state.Managed);
        var plan = planner.Plan(current, project, scopeInEffect);

        diagnostics.AddRange(plan);
        return diagnostics.ToResult(plan.Value);
    }

    public async Task<Result<StateCapture>> Refresh(MigrationPlan? applied = null, bool force = false, CancellationToken cancellationToken = default)
    {
        if (!stateManager.IsConfigured)
        {
            return Result.Failure<StateCapture>(WorkflowDiagnostics.StoreRequiredForRefresh);
        }

        progress.Report(OperationProgress.Step("Updating state store..."));
        var live = await databaseProvider.GetDatabase(PlanningScope.All, cancellationToken);
        if (live.Value is not { } schema)
        {
            return Result.Failure<StateCapture>(live.Diagnostics);
        }

        // Fetch the current state, apply this run's changes over it, and write the result back. A forced refresh
        // is the recovery path for an unreadable payload — it replaces it — but the replacement resets the
        // run-once ledger, so it must be asked for, and the capture flags it for the caller to surface.
        var read = await stateManager.Read(new StateReadArguments(), cancellationToken);
        if (read.IsFailure && !force)
        {
            return Result.Failure<StateCapture>(read.Diagnostics.Append(WorkflowDiagnostics.StateNotReplaced));
        }

        var state = read.Value?.State ?? DatabaseState.Empty;

        state = state with { Database = schema };
        if (applied is not null)
        {
            // Recording the run-once ledger is a state-domain job; the shell just supplies what ran and when.
            state = state.RecordExecution(applied.Diff.DeploymentScripts, DateTimeOffset.UtcNow)
                with
            { Managed = applied.Managed };
        }

        var written = await stateManager.Write(new StateWriteArguments(state), cancellationToken);
        if (written.Value is not { } write)
        {
            return Result.Failure<StateCapture>(written.Diagnostics);
        }
        progress.Report(OperationProgress.Detail($"State snapshot: {StatusHelpers.Describe(schema)}, {write.PayloadSize:N0} bytes."));

        var diagnostics = Enumerable.Empty<Diagnostic>();
        if (read.IsFailure)
        {
            // The read errors explain what was wrong with the replaced payload, but the forced capture succeeded,
            // so they ride along demoted to warnings — error severity would flip the result to a failure.
            diagnostics = read.Diagnostics
                .Select(d => d with { Severity = DiagnosticSeverity.Warning })
                .Append(WorkflowDiagnostics.LedgerReset);
        }

        return Result.Success(new StateCapture(schema, write.PayloadSize), diagnostics);
    }

    /// <summary>
    /// Resolves the state being planned towards: the declared project, or nothing for a teardown.
    /// </summary>
    private async Task<Result<ProjectDefinition>> ResolveTarget(PlanTarget target, PlanningScope scope, CancellationToken cancellationToken)
    {
        if (target == PlanTarget.Empty)
        {
            return Result.Success(new ProjectDefinition(new Database()));
        }

        progress.Report(OperationProgress.Step("Loading project..."));
        var loaded = await projectProvider.GetProject(scope, cancellationToken);
        if (loaded.Value is { } project)
        {
            ReportProjectDetail(project);
        }

        return loaded;
    }

    private static string Describe(PlanningScope scope) =>
        scope.IsUnscoped
            ? "(all)"
            : string.Join(", ", scope.Addresses.Select(a => a.Value));

    /// <summary>
    /// Emits the verbose census of what the loaded project declares.
    /// </summary>
    private void ReportProjectDetail(ProjectDefinition project)
    {
        var scriptCount = project.Directives.DeploymentScripts.Count + project.Directives.ChangeScripts.Count;
        progress.Report(OperationProgress.Detail($"Declared: {StatusHelpers.Describe(project.Database)}, " +
            $"{StatusHelpers.Count(scriptCount, "script")}."));
    }
}
