using NSchema.Services.Reporting;
using NSchema.Sql.Model;

namespace NSchema.Tests.Services;

/// <summary>
/// Snapshot coverage for <see cref="SqlPlanRenderer"/>.
/// </summary>
public sealed class SqlPlanRendererSnapshotTests
{
    [Fact]
    public Task Render_EmptyPlan() => Verify(SqlPlanRenderer.Render(new SqlPlan([])));

    [Fact]
    public Task Render_RichPlan()
    {
        var plan = new SqlPlan(
        [
            new SqlStatement("CREATE SCHEMA app"),
            new SqlStatement("CREATE TABLE app.users (\n    id int NOT NULL,\n    name text NOT NULL\n)"),
            new SqlStatement("CREATE INDEX CONCURRENTLY users_name_ix ON app.users (name)", RunOutsideTransaction: true),
            new SqlStatement("ANALYZE app.users"),
        ]);

        return Verify(SqlPlanRenderer.Render(plan));
    }
}
