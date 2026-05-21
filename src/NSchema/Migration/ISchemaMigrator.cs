using NSchema.Domain.Migration;

namespace NSchema.Migration;

public interface ISchemaMigrator
{
    Task Migrate(MigrationPlan plan, CancellationToken cancellationToken = default);
}
