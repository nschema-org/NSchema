using NSchema.Domain.Migration;

namespace NSchema.Migration.Execution;

public interface IInstructionExecutor
{
    Task Execute(
        IReadOnlyList<SchemaInstruction> instructions,
        ExecutionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}
