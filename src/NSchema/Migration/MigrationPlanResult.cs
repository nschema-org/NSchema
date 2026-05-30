using System.Diagnostics.CodeAnalysis;
using NSchema.Migration.Plan;
using NSchema.Policies;

namespace NSchema.Migration;

/// <summary>
/// The result of a planning pass: the computed plan plus all policy diagnostics.
/// </summary>
internal sealed record MigrationPlanResult(MigrationPlan? Plan, IReadOnlyList<PolicyError> Diagnostics)
{
    /// <summary>
    /// True when <see cref="Diagnostics"/> contains at least one error-severity finding.
    /// When false, <see cref="Plan"/> is guaranteed non-null.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Plan))]
    public bool HasErrors => Errors.Any();

    /// <summary>
    /// The subset of <see cref="Diagnostics"/> with error severity.
    /// </summary>
    public IEnumerable<PolicyError> Errors => Diagnostics.Where(d => d.Severity == PolicySeverity.Error);
}
