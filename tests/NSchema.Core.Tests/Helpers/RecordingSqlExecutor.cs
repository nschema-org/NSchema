using NSchema.Apply;
using NSchema.Plan.Domain;

namespace NSchema.Tests.Helpers;

/// <summary>Records the statements it was asked to execute, without touching a database.</summary>
internal sealed class RecordingSqlExecutor : ISqlExecutor
{
    public IReadOnlyList<SqlStatement>? Executed { get; private set; }

    public Task Execute(IReadOnlyList<SqlStatement> statements, CancellationToken cancellationToken = default)
    {
        Executed = statements;
        return Task.CompletedTask;
    }
}
