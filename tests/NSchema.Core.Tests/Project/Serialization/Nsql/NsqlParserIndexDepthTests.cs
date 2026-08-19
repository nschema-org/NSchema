using NSchema.Model.Indexes;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for the richer index grammar: <c>USING method</c>, <c>INCLUDE (cols)</c>, per-key
/// <c>ASC</c>/<c>DESC</c> and <c>NULLS FIRST</c>/<c>NULLS LAST</c>, and parenthesised expression keys.
/// </summary>
public sealed class NsqlParserIndexDepthTests
{
    private static TableIndex ParseIndex(string statement) =>
        new TestNsqlParser("CREATE TABLE app.t (a int, b int); " + statement).Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem();

    [Fact]
    public void Parse_Method_IsCaptured()
        => ParseIndex("CREATE INDEX t_a_ix ON app.t USING gin (a);").Method.ShouldBe("gin");

    [Fact]
    public void Parse_NoMethod_IsNull()
        => ParseIndex("CREATE INDEX t_a_ix ON app.t (a);").Method.ShouldBeNull();

    [Fact]
    public void Parse_Include_IsCaptured()
        => ParseIndex("CREATE INDEX t_a_ix ON app.t (a) INCLUDE (b);").Include.ShouldBe(["b"]);

    [Fact]
    public void Parse_AscDescAndNulls_AreCaptured()
    {
        // Act
        var keys = ParseIndex("CREATE INDEX t_ix ON app.t (a DESC NULLS LAST, b ASC NULLS FIRST);").Columns;

        // Assert
        keys[0].Column.ShouldBe("a");
        keys[0].Sort.ShouldBe(IndexSort.Descending);
        keys[0].Nulls.ShouldBe(IndexNulls.Last);
        keys[1].Sort.ShouldBe(IndexSort.Ascending);
        keys[1].Nulls.ShouldBe(IndexNulls.First);
    }

    [Fact]
    public void Parse_PlainColumn_HasDefaultOrdering()
    {
        var key = ParseIndex("CREATE INDEX t_a_ix ON app.t (a);").Columns.ShouldHaveSingleItem();
        key.Column.ShouldBe("a");
        key.Expression.ShouldBeNull();
        key.Sort.ShouldBe(IndexSort.Default);
        key.Nulls.ShouldBe(IndexNulls.Default);
    }

    [Fact]
    public void Parse_ExpressionKey_IsCaptured()
    {
        var key = ParseIndex("CREATE INDEX t_lower_ix ON app.t ((lower(a)));").Columns.ShouldHaveSingleItem();
        key.Column.ShouldBeNull();
        key.Expression.ShouldBe("lower(a)");
    }

    [Fact]
    public void Parse_InlineIndexWithMethodAndInclude_IsCaptured()
    {
        var index = new TestNsqlParser("CREATE TABLE app.t (a int, b int, INDEX t_ix USING gin (a) INCLUDE (b));").Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem();
        index.Method.ShouldBe("gin");
        index.Include.ShouldBe(["b"]);
    }

    [Fact]
    public void Parse_FullIndex_RoundTripsThroughWriter()
    {
        const string ddl = "CREATE TABLE app.t (a int, b int, c int);\n" +
            "CREATE UNIQUE INDEX t_ix ON app.t USING btree (c DESC NULLS LAST, (lower(a))) INCLUDE (b) WHERE (c IS NOT NULL);";
        var schema = new TestNsqlParser(ddl).Parse().Database;

        var reparsed = new TestNsqlParser(NsqlWriter.Write(schema)).Parse().Database
            .Schemas.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem();
        reparsed.IsUnique.ShouldBeTrue();
        reparsed.Method.ShouldBe("btree");
        reparsed.Include.ShouldBe(["b"]);
        reparsed.Predicate.ShouldBe("c IS NOT NULL");
        reparsed.Columns[0].ShouldBe(new IndexColumn("c", Sort: IndexSort.Descending, Nulls: IndexNulls.Last));
        reparsed.Columns[1].ShouldBe(new IndexColumn(Expression: "lower(a)"));
    }
}
