using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;
using NSchema.Schema.Model;
using NSchema.Sql.Model;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders an operation's structured output.
/// </summary>
internal interface IConsolePresenter
{
    /// <summary>
    /// Presents a single schema state as human-readable output (e.g. the recorded state for a show operation).
    /// </summary>
    void ReportSchema(DatabaseSchema schema);

    /// <summary>
    /// Presents the computed migration diff as human-readable output.
    /// </summary>
    void ReportDiff(DatabaseDiff diff);

    /// <summary>
    /// Presents plan-level detail that isn't part of the diff, such as the pre- and post-deployment scripts.
    /// </summary>
    void ReportPlan(MigrationPlan plan);

    /// <summary>
    /// Presents the SQL plan a migration would run.
    /// </summary>
    void ReportSqlPlan(SqlPlan plan);

    /// <summary>
    /// Presents a saved plan file as a single combined output.
    /// </summary>
    void ReportSavedPlan(PlanFileEnvelope envelope);
}
