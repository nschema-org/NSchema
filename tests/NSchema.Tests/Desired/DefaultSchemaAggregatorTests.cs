using NSchema.Migration;
using NSchema.Schema;
using DbSchema = NSchema.Schema.Schema;

namespace NSchema.Tests.Desired;

public sealed class DefaultSchemaAggregatorTests
{
    private static readonly DefaultSchemaAggregator s_aggregator = new();

    private static DatabaseSchema Db(params DbSchema[] schemas) =>
        new(schemas, [], []);

    private static DatabaseSchema Db(IReadOnlyList<DbSchema> schemas, IReadOnlyList<Script> pre, IReadOnlyList<Script> post) =>
        new(schemas, pre, post);

    private static DbSchema Schema(string name, params Table[] tables) =>
        new(name, tables);

    private static Table Table(string name) =>
        new(name, [], null, null, null, null);

    // ── Single provider ───────────────────────────────────────────────────────

    [Fact]
    public void Merge_SingleProvider_ReturnsSchemaUnchanged()
    {
        var db = Db(Schema("public", Table("users"), Table("posts")));

        var result = s_aggregator.Aggregate([db]);

        result.Schemas.Count.ShouldBe(1);
        result.Schemas[0].Name.ShouldBe("public");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    // ── Multiple providers, distinct schema names ─────────────────────────────

    [Fact]
    public void Merge_MultipleProviders_DistinctSchemaNames_ProducesAllSchemas()
    {
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("admin", Table("roles")));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas.Count.ShouldBe(2);
        result.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    // ── Multiple providers, same schema name ──────────────────────────────────

    [Fact]
    public void Merge_MultipleProviders_SameSchemaName_MergesTables()
    {
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("posts")));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas.Count.ShouldBe(1);
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public void Merge_DuplicateTableInSameSchema_Throws()
    {
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("users")));

        var ex = Should.Throw<InvalidOperationException>(() => s_aggregator.Aggregate([db1, db2]));
        ex.Message.ShouldContain("users");
        ex.Message.ShouldContain("public");
    }

    // ── Empty input ───────────────────────────────────────────────────────────

    [Fact]
    public void Merge_NoProviders_ReturnsEmptySchema()
    {
        var result = s_aggregator.Aggregate([]);

        result.Schemas.ShouldBeEmpty();
        result.PreDeploymentScripts.ShouldBeEmpty();
        result.PostDeploymentScripts.ShouldBeEmpty();
    }

    // ── Scripts ───────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_ConcatenatesPreDeploymentScripts()
    {
        var db1 = Db([], [new Script("a", "SELECT 1")], []);
        var db2 = Db([], [new Script("b", "SELECT 2")], []);

        var result = s_aggregator.Aggregate([db1, db2]);

        result.PreDeploymentScripts.Select(s => s.Name).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Merge_ConcatenatesPostDeploymentScripts()
    {
        var db1 = Db([], [], [new Script("seed1", "INSERT INTO t VALUES (1)")]);
        var db2 = Db([], [], [new Script("seed2", "INSERT INTO t VALUES (2)")]);

        var result = s_aggregator.Aggregate([db1, db2]);

        result.PostDeploymentScripts.Select(s => s.Name).ShouldBe(["seed1", "seed2"]);
    }

    [Fact]
    public void Merge_NoScripts_ScriptListsAreEmpty()
    {
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("admin", Table("roles")));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.PreDeploymentScripts.ShouldBeEmpty();
        result.PostDeploymentScripts.ShouldBeEmpty();
    }
}
