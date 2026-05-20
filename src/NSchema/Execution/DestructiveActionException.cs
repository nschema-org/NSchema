using NSchema.Domain.Execution;

namespace NSchema.Execution;

public sealed class DestructiveActionException(SchemaInstruction instruction)
    : Exception($"Destructive instruction blocked by policy: {instruction.GetType().Name}")
{
    public SchemaInstruction Instruction { get; } = instruction;
}
