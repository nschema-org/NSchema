using NSchema.Plan.Domain;

namespace NSchema.Plan.Policies;

/// <summary>
/// A plan policy that warns when a publication is renamed out from under its subscribers.
/// </summary>
internal sealed class PublicationRenamePolicy : IPlanPolicy
{
    public IEnumerable<Diagnostic> Validate(MigrationPlan plan)
    {
        foreach (var publication in plan.Diff.Publications)
        {
            if (publication.RenamedFrom is { } from)
            {
                yield return PublicationRenameDiagnostics.Renamed(from, publication.Name);
            }
        }
    }
}
