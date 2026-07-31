using NSchema.Diff.Domain;
using NSchema.Model;
using NSchema.Plan.Domain;
using NSchema.State.Domain;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders an operation's structured output.
/// </summary>
internal interface IConsolePresenter
{
    /// <summary>
    /// Presents a database schema.
    /// </summary>
    void ReportDatabase(Database database);

    /// <summary>
    /// Presents the recorded state.
    /// </summary>
    void ReportState(DatabaseState state);

    /// <summary>
    /// Presents the computed migration diff.
    /// </summary>
    void ReportDiff(DatabaseDiff diff);

    /// <summary>
    /// Presents the computed plan.
    /// </summary>
    void ReportPlan(MigrationPlan plan);

    /// <summary>
    /// Presents the script executions recorded in the state ledger.
    /// </summary>
    void ReportScripts(IReadOnlyList<ScriptExecution> scripts);
}
