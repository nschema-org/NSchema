namespace NSchema.Migration;

public interface ISchemaMigrator
{
    Task<MigrationPlan> Plan(CancellationToken cancellationToken = default);
    Task Apply(MigrationPlan plan, ExecutionOptions? options = null, CancellationToken cancellationToken = default);
}
