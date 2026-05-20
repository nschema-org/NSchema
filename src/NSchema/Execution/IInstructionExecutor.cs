using NSchema.Domain.Execution;

namespace NSchema.Execution;

public interface IInstructionExecutor
{
    Task Execute(
        IReadOnlyList<SchemaInstruction> instructions,
        ExecutionOptions? options = null,
        CancellationToken cancellationToken = default);
}
