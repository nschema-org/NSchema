using NSchema.Migration.Plan;

namespace NSchema.Migration.Sql;

/// <summary>
/// A compiled migration that executes a SQL plan.
/// </summary>
/// <param name="plan">The migration plan this unit was compiled from.</param>
/// <param name="sqlPlan">The compiled SQL plan.</param>
/// <param name="sqlExecutor">The executor that runs the SQL plan.</param>
internal sealed class CompiledSqlMigration(MigrationPlan plan, SqlPlan sqlPlan, ISqlExecutor sqlExecutor) : ICompiledMigration
{
    /// <inheritdoc />
    public MigrationPlan Plan { get; } = plan;

    /// <inheritdoc />
    public IReadOnlyList<string> Preview { get; } = sqlPlan.Statements.Select(s => s.Sql).ToArray();

    /// <inheritdoc />
    public Task Execute(CancellationToken cancellationToken = default) => sqlExecutor.Execute(sqlPlan, cancellationToken);
}
