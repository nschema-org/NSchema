using NSchema.Plan.Domain;

namespace NSchema.Operations;

/// <summary>
/// The result of applying a plan.
/// </summary>
/// <param name="AppliedPlan">The plan that was applied. An empty plan means the recorded state already matched the target.</param>
public sealed record ApplyResult(MigrationPlan AppliedPlan)
{
    /// <summary>
    /// Whether the apply changed anything.
    /// </summary>
    public bool ChangesApplied => !AppliedPlan.IsEmpty;

    /// <summary>
    /// The number of SQL statements that were executed.
    /// </summary>
    public int StatementsExecuted => AppliedPlan.Statements.Count;
}
