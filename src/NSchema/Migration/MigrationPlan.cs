using NSchema.Migration.Actions;

namespace NSchema.Migration;

public sealed record MigrationPlan(IReadOnlyList<MigrationAction> Actions)
{
    public bool IsEmpty => Actions.Count == 0;
}
