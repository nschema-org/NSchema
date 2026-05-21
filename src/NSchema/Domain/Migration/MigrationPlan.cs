using NSchema.Domain.Migration.Actions;

namespace NSchema.Domain.Migration;

public sealed record MigrationPlan(IReadOnlyList<SchemaAction> Actions)
{
    public bool IsEmpty => Actions.Count == 0;
}
