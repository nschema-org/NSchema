namespace NSchema.Tests.Project.Model;

/// <summary>
/// Snapshot coverage for cross-file assembly. The per-case assertions in
/// <see cref="NSchema.Tests.Project.ProjectAssemblerTests"/> pin the merge rules; this captures the whole
/// merged <c>Database</c> graph for several files at once, so table grouping, ordering, and
/// carried-through detail all show up as a single reviewable diff.
/// </summary>
public sealed class DatabaseSnapshotTests
{
    [Fact]
    public Task Assemble_MultipleFiles_MergesIntoSingleGraph()
    {
        // Two files contribute to "app" (tables and views merged); a third owns "reporting" on its own.
        const string core = """
            --- application schema
            CREATE SCHEMA app;
            CREATE TABLE app.users (id bigint, name varchar(255), CONSTRAINT users_pkey PRIMARY KEY (id));
            --- currently active users
            CREATE VIEW app.active_users AS SELECT id, name FROM app.users;
            """;

        const string billing = """
            CREATE TABLE app.invoices (id bigint, amount decimal(18, 2));
            CREATE VIEW app.invoice_totals AS SELECT id, amount FROM app.invoices;
            """;

        const string reporting = """
            --- analytics
            CREATE SCHEMA reporting;
            CREATE TABLE reporting.daily_totals (day date);
            CREATE VIEW reporting.weekly_rollup AS SELECT day FROM reporting.daily_totals;
            """;

        return Verify(TestNsqlParser.Assemble(core, billing, reporting).Require().Database);
    }
}
