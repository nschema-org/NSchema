using NSchema.Model.Services;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Pins the shallow FROM/JOIN dependency scan that feeds the planner's view ordering. The scan over-collects by
/// design (a spurious name simply matches no planned object), so these tests focus on the cases that matter:
/// genuine references must always be found, and CTE-local names must not be.
/// </summary>
public sealed class ViewDependencyExtractorTests
{
    private static (string Schema, string Name)[] Extract(string body, string defaultSchema = "app")
        => ViewDependencyExtractor.Extract(body, defaultSchema).Select(d => (d.Schema.Value, d.Name.Value)).ToArray();

    [Fact]
    public void Extract_SimpleFrom_FindsQualifiedTable()
        => Extract("SELECT id FROM app.users").ShouldBe([("app", "users")]);

    [Fact]
    public void Extract_UnqualifiedName_ResolvesAgainstDefaultSchema()
        => Extract("SELECT id FROM users", "sales").ShouldBe([("sales", "users")]);

    [Fact]
    public void Extract_Join_FindsBothSides()
        => Extract("SELECT * FROM app.orders o JOIN app.customers c ON o.cid = c.id")
            .ShouldBe([("app", "orders"), ("app", "customers")]);

    [Fact]
    public void Extract_CommaSeparatedFrom_FindsAll()
        => Extract("SELECT * FROM app.a, app.b, app.c")
            .ShouldBe([("app", "a"), ("app", "b"), ("app", "c")]);

    [Fact]
    public void Extract_Subquery_FindsNestedReference()
        => Extract("SELECT * FROM (SELECT id FROM app.inner_t) sub JOIN app.outer_t t ON t.id = sub.id")
            .ShouldBe([("app", "inner_t"), ("app", "outer_t")]);

    [Fact]
    public void Extract_Cte_ExcludesCteNameButKeepsRealSource()
    {
        // Act
        // active reads app.users; the outer query reads the CTE (local) and app.orders (real).
        var deps = Extract("WITH active AS (SELECT id FROM app.users) SELECT * FROM active JOIN app.orders o ON o.uid = active.id");

        // Assert
        deps.ShouldBe([("app", "users"), ("app", "orders")]);
    }

    [Fact]
    public void Extract_NestedCtes_ExcludesAllCteNames()
    {
        // Arrange
        var deps = Extract(

            // Act
            "WITH a AS (SELECT 1 FROM app.t1), b AS (SELECT 1 FROM a JOIN app.t2 x ON true) SELECT * FROM b");

        // Assert
        deps.ShouldBe([("app", "t1"), ("app", "t2")]);
    }

    [Fact]
    public void Extract_IgnoresStringLiteralsAndStar()
        => Extract("SELECT *, 'FROM not_a_table' AS label FROM app.real_t")
            .ShouldBe([("app", "real_t")]);

    [Fact]
    public void Extract_Deduplicates()
        => Extract("SELECT * FROM app.t JOIN app.t t2 ON t.a = t2.b")
            .ShouldBe([("app", "t")]);
}
