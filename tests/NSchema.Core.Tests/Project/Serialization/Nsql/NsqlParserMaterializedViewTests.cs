using NSchema.Model.Views;
using NSchema.Project.Domain.Directives;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Parser coverage for <c>CREATE MATERIALIZED VIEW</c> and the standalone <c>CREATE [UNIQUE] INDEX … ON s.v</c>
/// that attaches to a materialized view at build time (like a trigger attaches to its table).
/// </summary>
public sealed class NsqlParserMaterializedViewTests
{
    private static View ParseView(string sql) =>
        new TestNsqlParser("CREATE SCHEMA app; " + sql).Parse().Database
            .Schemas.ShouldHaveSingleItem().Views.ShouldHaveSingleItem();

    [Fact]
    public void Parse_MaterializedView_SetsFlag()
    {
        // Act
        var view = ParseView("CREATE MATERIALIZED VIEW app.daily AS SELECT 1;");

        // Assert
        view.Name.ShouldBe("daily");
        view.IsMaterialized.ShouldBeTrue();
        view.Body.ShouldBe("SELECT 1");
    }

    [Fact]
    public void Parse_PlainView_IsNotMaterialized()
        => ParseView("CREATE VIEW app.v AS SELECT 1;").IsMaterialized.ShouldBeFalse();

    [Fact]
    public void Parse_StandaloneIndexOnMaterializedView_Attaches()
    {
        // Arrange
        var view = ParseView(

            // Act
            "CREATE MATERIALIZED VIEW app.daily AS SELECT date FROM app.t; CREATE INDEX daily_ix ON app.daily (date);");

        // Assert
        var index = view.Indexes.ShouldHaveSingleItem();
        index.Name.ShouldBe("daily_ix");
        index.Columns.Select(c => c.Column?.Value).ShouldBe(["date"]);
        index.IsUnique.ShouldBeFalse();
    }

    [Fact]
    public void Parse_UniqueIndexWithPredicate_OnMaterializedView()
    {
        var index = ParseView(
            "CREATE MATERIALIZED VIEW app.daily AS SELECT date FROM app.t; " +
            "CREATE UNIQUE INDEX daily_ix ON app.daily (date) WHERE (date IS NOT NULL);").Indexes.ShouldHaveSingleItem();
        index.IsUnique.ShouldBeTrue();
        index.Predicate.ShouldBe("date IS NOT NULL");
    }

    [Fact]
    public void Parse_IndexBeforeItsMaterializedView_StillAttaches()
        // Build-time resolution: the index may be declared before the matview it targets.
        => ParseView("CREATE INDEX daily_ix ON app.daily (x); CREATE MATERIALIZED VIEW app.daily AS SELECT x FROM app.t;")
            .Indexes.ShouldHaveSingleItem().Name.ShouldBe("daily_ix");

    [Fact]
    public void Parse_IndexOnPlainView_FailsTheRead()
        => new TestNsqlParser("CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1; CREATE INDEX ix ON app.v (x);").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("not a materialized view");

    [Fact]
    public void Parse_IndexOnUnknownRelation_FailsTheRead()
        => new TestNsqlParser("CREATE SCHEMA app; CREATE INDEX ix ON app.ghost (x);").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("unknown table or materialized view");

    [Fact]
    public void Parse_DuplicateIndexOnView_FailsTheRead()
        => new TestNsqlParser(
            "CREATE SCHEMA app; CREATE MATERIALIZED VIEW app.m AS SELECT x FROM app.t; " +
            "CREATE INDEX ix ON app.m (x); CREATE INDEX ix ON app.m (y);").Project().Errors.ShouldHaveSingleItem()
            .Message.ShouldContain("already declared");

    private static ProjectDirectives Directives(string source)
    {
        var read = NSchema.Project.Nsql.NsqlReader.Read(source);
        read.IsSuccess.ShouldBeTrue();
        return NSchema.Project.ProjectAssembler.Assemble([read.Value]).Value!.Directives;
    }
}
