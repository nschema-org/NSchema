using NSchema.Hosting;
using NSchema.Migration.Plan;

namespace NSchema.Migration.Sql;

/// <summary>
/// Default <see cref="IMigrationExecutor"/> for SQL targets.
/// </summary>
internal sealed class SqlMigrationExecutor(
    IMigrationReporter reporter,
    ISqlPlanner sqlPlanner,
    ISqlExecutor sqlExecutor
) : IMigrationExecutor
{
    public async Task Apply(MigrationPlan plan, bool planOnly, CancellationToken cancellationToken = default)
    {
        reporter.Info("Generating SQL statements...");
        var sqlPlan = sqlPlanner.Plan(plan);
        foreach (var statement in sqlPlan.Statements)
        {
            reporter.Info(statement.Sql);
        }

        if (planOnly)
        {
            return;
        }

        reporter.Info("Running database migration...");
        await sqlExecutor.Execute(sqlPlan, cancellationToken);
    }
}
