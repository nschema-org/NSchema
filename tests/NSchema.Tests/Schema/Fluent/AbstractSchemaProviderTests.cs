using NSchema.Schema;
using NSchema.Schema.Fluent;

namespace NSchema.Tests.Schema.Fluent;

public sealed class AbstractSchemaProviderTests
{
    private sealed class TestSchemaProvider : AbstractSchemaProvider;

    private readonly TestSchemaProvider _sut = new();

    // ── DatabaseModelBuilder ──────────────────────────────────────────────────

    [Fact]
    public async Task Build_WithNoSchemas_ReturnsModelWithEmptySchemaList()
    {
        // Arrange

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Schema_AddsSchemaToModel()
    {
        // Arrange
        _sut.Schema("public");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas.Count.ShouldBe(1);
        model.Schemas[0].Name.ShouldBe("public");
    }

    [Fact]
    public async Task Schema_MultipleSchemas_AllAppearInModel()
    {
        // Arrange
        _sut.Schema("public");
        _sut.Schema("admin");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas.Count.ShouldBe(2);
        model.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    // ── SchemaBuilder ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SchemaBuilder_Table_AddsTableToSchema()
    {
        // Arrange
        _sut.Schema("public").Table("users");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables.Count.ShouldBe(1);
        model.Schemas[0].Tables[0].Name.ShouldBe("users");
    }

    [Fact]
    public async Task SchemaBuilder_MultipleTables_AllAppearInSchema()
    {
        // Arrange
        var schema = _sut.Schema("public");
        schema.Table("users");
        schema.Table("posts");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public async Task SchemaBuilder_RenamedFrom_SetsOldNameOnSchema()
    {
        // Arrange
        _sut.Schema("public").RenamedFrom("old_schema");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].OldName.ShouldBe("old_schema");
    }

    // ── TableBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TableBuilder_Column_AddsColumnToTable()
    {
        // Arrange
        var table = _sut.Schema("public").Table("users");
        table.Column("id", SqlType.BigInt);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        var t = model.Schemas[0].Tables[0];
        t.Columns.Count.ShouldBe(1);
        t.Columns[0].Name.ShouldBe("id");
        t.Columns[0].Type.ShouldBe(SqlType.BigInt);
    }

    [Fact]
    public async Task TableBuilder_PrimaryKey_AttachesPrimaryKeyToTable()
    {
        // Arrange
        var table = _sut.Schema("public").Table("users");
        table.Column("id", SqlType.BigInt).NotNull();
        table.PrimaryKey("pk_users", ["id"]);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        var t = model.Schemas[0].Tables[0];
        t.PrimaryKey.ShouldNotBeNull();
        t.PrimaryKey!.Name.ShouldBe("pk_users");
        t.PrimaryKey.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public async Task TableBuilder_ForeignKey_AddsForeignKeyToTable()
    {
        // Arrange
        var table = _sut.Schema("public").Table("posts");
        table.Column("user_id", SqlType.BigInt);
        table.ForeignKey("fk_posts_user", ["user_id"], "public", "users", ["id"]);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        var t = model.Schemas[0].Tables[0];
        t.ForeignKeys.Count.ShouldBe(1);
        t.ForeignKeys[0].Name.ShouldBe("fk_posts_user");
        t.ForeignKeys[0].ColumnNames.ShouldBe(["user_id"]);
        t.ForeignKeys[0].ReferencedSchema.ShouldBe("public");
        t.ForeignKeys[0].ReferencedTable.ShouldBe("users");
        t.ForeignKeys[0].ReferencedColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public async Task TableBuilder_Index_AddsIndexToTable()
    {
        // Arrange
        var table = _sut.Schema("public").Table("users");
        table.Column("email", SqlType.Text);
        table.Index("idx_users_email", ["email"]);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        var t = model.Schemas[0].Tables[0];
        t.Indexes!.Count.ShouldBe(1);
        t.Indexes[0].Name.ShouldBe("idx_users_email");
        t.Indexes[0].ColumnNames.ShouldBe(["email"]);
        t.Indexes[0].IsUnique.ShouldBeFalse();
    }

    [Fact]
    public async Task TableBuilder_RenamedFrom_SetsOldNameOnTable()
    {
        // Arrange
        _sut.Schema("public").Table("users").RenamedFrom("members");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].OldName.ShouldBe("members");
    }

    [Fact]
    public async Task TableBuilder_NoForeignKeys_ForeignKeysIsEmpty()
    {
        // Arrange
        var table = _sut.Schema("public").Table("users");
        table.Column("id", SqlType.BigInt);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].ForeignKeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task TableBuilder_NoIndexes_IndexesIsEmpty()
    {
        // Arrange
        var table = _sut.Schema("public").Table("users");
        table.Column("id", SqlType.BigInt);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Indexes.ShouldBeEmpty();
    }

    // ── ColumnBuilder ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ColumnBuilder_Defaults_IsNullableWithNoIdentityOrDefault()
    {
        // Arrange
        _sut.Schema("public").Table("t").Column("col", SqlType.Text);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        var column = model.Schemas[0].Tables[0].Columns[0];
        column.IsNullable.ShouldBeTrue();
        column.IsIdentity.ShouldBeFalse();
        column.DefaultExpression.ShouldBeNull();
        column.OldName.ShouldBeNull();
    }

    [Fact]
    public async Task ColumnBuilder_NotNull_SetsIsNullableFalse()
    {
        // Arrange
        _sut.Schema("public").Table("t").Column("col", SqlType.Text).NotNull();

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Columns[0].IsNullable.ShouldBeFalse();
    }

    [Fact]
    public async Task ColumnBuilder_Nullable_SetsIsNullableTrue()
    {
        // Arrange
        _sut.Schema("public").Table("t").Column("col", SqlType.Text).NotNull().Nullable();

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Columns[0].IsNullable.ShouldBeTrue();
    }

    [Fact]
    public async Task ColumnBuilder_Identity_SetsIsIdentityTrue()
    {
        // Arrange
        _sut.Schema("public").Table("t").Column("id", SqlType.BigInt).Identity();

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Columns[0].IsIdentity.ShouldBeTrue();
    }

    [Fact]
    public async Task ColumnBuilder_Default_SetsDefaultExpression()
    {
        // Arrange
        _sut.Schema("public").Table("t").Column("created_at", SqlType.DateTimeOffset).Default("now()");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Columns[0].DefaultExpression.ShouldBe("now()");
    }

    [Fact]
    public async Task ColumnBuilder_RenamedFrom_SetsOldName()
    {
        // Arrange
        _sut.Schema("public").Table("t").Column("full_name", SqlType.Text).RenamedFrom("name");

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Columns[0].OldName.ShouldBe("name");
    }

    // ── ForeignKeyBuilder ─────────────────────────────────────────────────────

    [Fact]
    public async Task ForeignKeyBuilder_OnDelete_SetsDeleteAction()
    {
        // Arrange
        _sut.Schema("public").Table("posts")
            .ForeignKey("fk", ["user_id"], "public", "users", ["id"])
            .OnDelete(ReferentialAction.Cascade);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].ForeignKeys[0].OnDelete.ShouldBe(ReferentialAction.Cascade);
    }

    [Fact]
    public async Task ForeignKeyBuilder_OnUpdate_SetsUpdateAction()
    {
        // Arrange
        _sut.Schema("public").Table("posts")
            .ForeignKey("fk", ["user_id"], "public", "users", ["id"])
            .OnUpdate(ReferentialAction.SetNull);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].ForeignKeys[0].OnUpdate.ShouldBe(ReferentialAction.SetNull);
    }

    [Fact]
    public async Task ForeignKeyBuilder_Defaults_NoActionForBothRules()
    {
        // Arrange
        _sut.Schema("public").Table("posts")
            .ForeignKey("fk", ["user_id"], "public", "users", ["id"]);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        var fk = model.Schemas[0].Tables[0].ForeignKeys[0];
        fk.OnDelete.ShouldBe(ReferentialAction.NoAction);
        fk.OnUpdate.ShouldBe(ReferentialAction.NoAction);
    }

    // ── IndexBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IndexBuilder_Unique_SetsIsUniqueTrue()
    {
        // Arrange
        _sut.Schema("public").Table("users")
            .Index("idx_email", ["email"])
            .Unique();

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Indexes[0].IsUnique.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexBuilder_CompositeIndex_PreservesColumnOrder()
    {
        // Arrange
        _sut.Schema("public").Table("t")
            .Index("idx_composite", ["last_name", "first_name"]);

        // Act
        var model = await _sut.GetSchema([]);

        // Assert
        model.Schemas[0].Tables[0].Indexes[0].ColumnNames.ShouldBe(["last_name", "first_name"]);
    }
}
