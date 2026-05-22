namespace NSchema.Migration;

public interface ISqlPlanner
{
    SqlPlan Plan(MigrationPlan plan);
}
