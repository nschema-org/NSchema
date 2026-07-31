using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that reports the objects an apply takes over: management changes hands, but no statement
/// says so, so an otherwise empty plan would adopt them silently.
/// </summary>
internal sealed class AdoptionPolicy : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan) =>
        plan.Adopted.IsEmpty ? [] : [AdoptionDiagnostics.ObjectsAdopted(plan.Adopted)];
}
