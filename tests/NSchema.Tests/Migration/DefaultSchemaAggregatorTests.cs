using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public sealed class DefaultSchemaAggregatorTests
{
    private readonly DefaultSchemaAggregator _sut = new();

    private static DatabaseSchema Db(params SchemaDefinition[] schemas) => DatabaseSchema.Create(schemas);

    private static SchemaDefinition Schema(string name, params Table[] tables) => SchemaDefinition.Create(name, tables: tables);

    private static Table Table(string name) => NSchema.Schema.Table.Create(name);

    // ── Single provider ───────────────────────────────────────────────────────

    [Fact]
    public void Merge_SingleProvider_ReturnsSchemaUnchanged()
    {
        // Arrange
        var db = Db(Schema("public", Table("users"), Table("posts")));

        // Act
        var result = _sut.Aggregate([db]);

        // Assert
        result.Schemas.Count.ShouldBe(1);
        result.Schemas[0].Name.ShouldBe("public");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    // ── Multiple providers, distinct schema names ─────────────────────────────

    [Fact]
    public void Merge_MultipleProviders_DistinctSchemaNames_ProducesAllSchemas()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("admin", Table("roles")));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas.Count.ShouldBe(2);
        result.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    // ── Multiple providers, same schema name ──────────────────────────────────

    [Fact]
    public void Merge_MultipleProviders_SameSchemaName_MergesTables()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas.Count.ShouldBe(1);
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public void Merge_DuplicateTableInSameSchema_Throws()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("users")));

        // Act
        var act = () => _sut.Aggregate([db1, db2]);

        // Assert
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("users");
        ex.Message.ShouldContain("public");
    }

    // ── Empty input ───────────────────────────────────────────────────────────

    [Fact]
    public void Merge_NoProviders_ReturnsEmptySchema()
    {
        // Arrange

        // Act
        var result = _sut.Aggregate([]);

        // Assert
        result.Schemas.ShouldBeEmpty();
    }

    // ── Partial schemas ───────────────────────────────────────────────────────

    [Fact]
    public void Merge_AnyProviderPartial_ResultIsPartial()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", isPartial: true));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].IsPartial.ShouldBeTrue();
    }

    [Fact]
    public void Merge_NoProviderPartial_ResultIsNotPartial()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].IsPartial.ShouldBeFalse();
    }

    [Fact]
    public void Merge_DroppedTables_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", droppedTables: ["old_users"]));
        var db2 = Db(SchemaDefinition.Create("public", droppedTables: ["legacy_data"]));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].DroppedTables.ShouldBe(["old_users", "legacy_data"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_DroppedSchemas_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = DatabaseSchema.Create([], ["old_schema"]);
        var db2 = DatabaseSchema.Create([], ["legacy"]);

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.DroppedSchemas.ShouldBe(["old_schema", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_NoDroppedSchemas_DroppedSchemasIsNull()
    {
        // Arrange
        var db1 = Db(Schema("public", Table("users")));

        // Act
        var result = _sut.Aggregate([db1]);

        // Assert
        result.DroppedSchemas.ShouldBeEmpty();
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Comment_FromSingleProvider_IsPreserved()
    {
        // Arrange
        var db = Db(SchemaDefinition.Create("public", comment: "App schema"));

        // Act
        var result = _sut.Aggregate([db]);

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Merge_Comment_FromOneOfMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", comment: "App schema"));
        var db2 = Db(Schema("public", Table("posts")));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
        result.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["posts"]);
    }

    [Fact]
    public void Merge_SameCommentFromMultipleProviders_IsPreserved()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", comment: "App schema"));
        var db2 = Db(SchemaDefinition.Create("public", comment: "App schema"));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].Comment.ShouldBe("App schema");
    }

    [Fact]
    public void Merge_ConflictingComments_Throws()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", comment: "App schema"));
        var db2 = Db(SchemaDefinition.Create("public", comment: "Different comment"));

        // Act
        var act = () => _sut.Aggregate([db1, db2]);

        // Assert
        var ex = act.ShouldThrow<InvalidOperationException>();
        ex.Message.ShouldContain("public");
    }

    // ── Grants ────────────────────────────────────────────────────────────────

    [Fact]
    public void Merge_Grants_FromSingleProvider_ArePreserved()
    {
        // Arrange
        var db = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));

        // Act
        var result = _sut.Aggregate([db]);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }

    [Fact]
    public void Merge_Grants_AreCombinedAcrossProviders()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));
        var db2 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("reporting")]));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user", "reporting"], ignoreOrder: true);
    }

    [Fact]
    public void Merge_DuplicateGrants_AreDeduplicated()
    {
        // Arrange
        var db1 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));
        var db2 = Db(SchemaDefinition.Create("public", grants: [new SchemaGrant("app_user")]));

        // Act
        var result = _sut.Aggregate([db1, db2]);

        // Assert
        result.Schemas[0].Grants.Select(g => g.Role).ShouldBe(["app_user"]);
    }
}
