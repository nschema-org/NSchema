using NSchema.Migration.Plan;

namespace NSchema.Migration.Sql;

/// <summary>
/// Default <see cref="IMigrationCompiler"/> for SQL targets.
/// </summary>
internal sealed class SqlMigrationCompiler(ISqlPlanner sqlPlanner, ISqlExecutor sqlExecutor) : IMigrationCompiler
{
    public Task<ICompiledMigration> Compile(MigrationPlan plan, CancellationToken cancellationToken = default)
    {
        var sqlPlan = sqlPlanner.Plan(plan);
        return Task.FromResult<ICompiledMigration>(new CompiledSqlMigration(sqlPlan, sqlExecutor));
    }
}
