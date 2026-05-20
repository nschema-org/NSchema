using NSchema.Domain.Schema;
using NSchema.Migration.Execution;

namespace NSchema.Migration;

public interface ISchemaMigrator
{
    Task<MigrationPlan> Plan(DatabaseModel target, CancellationToken cancellationToken = default);
    Task Apply(MigrationPlan plan, ExecutionOptions? options = null, CancellationToken cancellationToken = default);
}
