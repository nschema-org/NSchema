using NSchema.Domain.Migration;

namespace NSchema.Migration;

public sealed record MigrationPlan(IReadOnlyList<SchemaInstruction> Instructions)
{
    public bool IsEmpty => Instructions.Count == 0;
    public bool HasDestructiveInstructions => Instructions.Any(i => i.IsDestructive);
}
