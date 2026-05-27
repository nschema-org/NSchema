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
    public async Task Execute(MigrationPlan plan, bool dryRun, CancellationToken cancellationToken = default)
    {
        reporter.Info("Generating SQL statements...");
        var sqlPlan = sqlPlanner.Plan(plan);
        foreach (var statement in sqlPlan.Statements)
        {
            reporter.Info(statement.Sql);
        }

        if (dryRun)
        {
            return;
        }

        reporter.Info("Running database migration...");
        await sqlExecutor.Execute(sqlPlan, cancellationToken);
    }
}
