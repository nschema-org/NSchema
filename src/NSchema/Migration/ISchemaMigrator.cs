namespace NSchema.Migration;

public interface ISchemaMigrator
{
    Task<MigrationPlan> Plan(CancellationToken cancellationToken = default);
}
