using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Plan.Model;
using NSchema.Plan.PlanFile;

namespace NSchema.Services.Reporting;

/// <summary>
/// Renders an operation's structured output.
/// </summary>
internal interface IConsolePresenter
{
    /// <summary>
    /// Presents a single database schema as human-readable output (e.g. the recorded state for a show operation).
    /// </summary>
    void ReportSchema(Database database);

    /// <summary>
    /// Presents the computed migration diff as human-readable output.
    /// </summary>
    void ReportDiff(DatabaseDiff diff);

    /// <summary>
    /// Presents the SQL statements a migration would run.
    /// </summary>
    void ReportSqlPlan(IReadOnlyList<SqlStatement> statements);

    /// <summary>
    /// Presents a saved plan file as a single combined output.
    /// </summary>
    void ReportSavedPlan(PlanFileEnvelope envelope);
}
