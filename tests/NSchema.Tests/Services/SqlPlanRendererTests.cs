using NSchema.Services.Reporting;
using NSchema.Sql.Model;

namespace NSchema.Tests.Services;

public sealed class SqlPlanRendererTests
{
    [Fact]
    public void Render_EmptyPlan_ReportsNothingToExecute()
    {
        var output = SqlPlanRenderer.Render(new SqlPlan([]));

        output.ShouldContain("SQL Preview:");
        output.ShouldContain("No statements to execute");
    }

    [Fact]
    public void Render_IncludesEachStatementInOrder()
    {
        var plan = new SqlPlan([new SqlStatement("CREATE SCHEMA app"), new SqlStatement("CREATE TABLE app.users (id int)")]);

        var output = SqlPlanRenderer.Render(plan);

        output.ShouldContain("CREATE SCHEMA app");
        output.ShouldContain("CREATE TABLE app.users (id int)");
        output.IndexOf("CREATE SCHEMA app").ShouldBeLessThan(output.IndexOf("CREATE TABLE app.users (id int)"));
    }

    [Fact]
    public void Render_NumbersStatements()
    {
        var plan = new SqlPlan([new SqlStatement("SELECT 1"), new SqlStatement("SELECT 2")]);

        var output = SqlPlanRenderer.Render(plan);

        output.ShouldContain("[1/2]");
        output.ShouldContain("[2/2]");
    }

    [Fact]
    public void Render_FlagsStatementsThatRunOutsideTransaction()
    {
        var plan = new SqlPlan(
        [
            new SqlStatement("CREATE INDEX CONCURRENTLY ix ON app.users (id)", RunOutsideTransaction: true),
            new SqlStatement("ANALYZE app.users"),
        ]);

        var lines = SqlPlanRenderer.Render(plan).Split('\n');
        var concurrentHeader = lines.First(l => l.Contains("[1/2]"));
        var analyzeHeader = lines.First(l => l.Contains("[2/2]"));

        concurrentHeader.ShouldContain("outside transaction");
        analyzeHeader.ShouldNotContain("outside transaction");
    }
}
