using NSchema.Domain.Migration;

namespace NSchema.Migration;

public sealed class DestructiveActionException(SchemaInstruction instruction)
    : Exception($"Destructive instruction blocked by policy: {instruction.GetType().Name}")
{
    public SchemaInstruction Instruction { get; } = instruction;
}
