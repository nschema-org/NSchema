using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public sealed class DefaultSchemaAggregatorTests
{
    private static readonly DefaultSchemaAggregator s_aggregator = new();

    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => new(schemas);

    private static SchemaDefinition Schema(string name, params Table[] tables) => new(name, Tables: tables);

    private static Table Table(string name) => new(name);

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
    }

    // ── Partial schemas ───────────────────────────────────────────────────────

    [Fact]
    public void Merge_AnyProviderPartial_ResultIsPartial()
    {
        var db1 = Db(new SchemaDefinition("public", IsPartial: true));
        var db2 = Db(Schema("public", Table("posts")));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].IsPartial.ShouldBeTrue();
    }

    [Fact]
    public void Merge_NoProviderPartial_ResultIsNotPartial()
    {
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("posts")));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].IsPartial.ShouldBeFalse();
    }

    [Fact]
    public void Merge_DroppedTables_AreCombinedAcrossProviders()
    {
        var db1 = Db(new SchemaDefinition("public", DroppedTables: ["old_users"]));
        var db2 = Db(new SchemaDefinition("public", DroppedTables: ["legacy_data"]));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].DroppedTables.ShouldBe(["old_users", "legacy_data"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_DroppedSchemas_AreCombinedAcrossProviders()
    {
        var db1 = new DatabaseSchema([], ["old_schema"]);
        var db2 = new DatabaseSchema([], ["legacy"]);

        var result = s_aggregator.Aggregate([db1, db2]);

        result.DroppedSchemas.ShouldBe(["old_schema", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_NoDroppedSchemas_DroppedSchemasIsNull()
    {
        var db1 = Db(Schema("public", Table("users")));

        var result = s_aggregator.Aggregate([db1]);

        result.DroppedSchemas.ShouldBeEmpty();
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Comment_FromSingleProvider_IsPreserved()
    {
        var db = Db(new SchemaDefinition("public", Comment: "App schema"));

        var result = s_aggregator.Aggregate([db]);

        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Merge_Comment_FromOneOfMultipleProviders_IsPreserved()
    {
        var db1 = Db(new SchemaDefinition("public", Comment: "App schema"));
        var db2 = Db(Schema("public", Table("posts")));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].Comment.ShouldBe("App schema");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["posts"]);
    }

    [Fact]
    public void Merge_SameCommentFromMultipleProviders_IsPreserved()
    {
        var db1 = Db(new SchemaDefinition("public", Comment: "App schema"));
        var db2 = Db(new SchemaDefinition("public", Comment: "App schema"));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Merge_ConflictingComments_Throws()
    {
        var db1 = Db(new SchemaDefinition("public", Comment: "App schema"));
        var db2 = Db(new SchemaDefinition("public", Comment: "Different comment"));

        var ex = Should.Throw<InvalidOperationException>(() => s_aggregator.Aggregate([db1, db2]));
        ex.Message.ShouldContain("public");
    }

    // ── Grants ────────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Grants_FromSingleProvider_ArePreserved()
    {
        var db = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));

        var result = s_aggregator.Aggregate([db]);

        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }

    [Fact]
    public void Merge_Grants_AreCombinedAcrossProviders()
    {
        var db1 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));
        var db2 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("reporting")]));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_DuplicateGrants_AreDeduplicated()
    {
        var db1 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));
        var db2 = Db(new SchemaDefinition("public", Grants: [new SchemaGrant("app_user")]));

        var result = s_aggregator.Aggregate([db1, db2]);

        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }
}
