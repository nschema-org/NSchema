using NSchema.Domain.Migration;

namespace NSchema.Migration;

public interface IMigrationPlanTransformer
{
    MigrationPlan Transform(MigrationPlan plan);
}
