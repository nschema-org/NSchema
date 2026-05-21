using NSchema.Domain.Schema;
using NSchema.Domain.Schema.Fluent;

namespace NSchema.Tests.Desired.Fluent;

public sealed class AbstractSchemaProviderTests
{
    private sealed class TestSchemaProvider : AbstractSchemaProvider;

    private static async Task<DatabaseSchema> Build(Action<TestSchemaProvider> configure)
    {
        var provider = new TestSchemaProvider();
        configure(provider);
        return await provider.GetSchema();
    }

    // ── DatabaseModelBuilder ──────────────────────────────────────────────────

    [Fact]
    public async Task Build_WithNoSchemas_ReturnsModelWithEmptySchemaList()
    {
        var model = await Build(_ => { });
        model.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public async Task Schema_AddsSchemaToModel()
    {
        var model = await Build(p => p.Schema("public"));

        model.Schemas.Count.ShouldBe(1);
        model.Schemas[0].Name.ShouldBe("public");
    }

    [Fact]
    public async Task Schema_MultipleSchemas_AllAppearInModel()
    {
        var model = await Build(p =>
        {
            p.Schema("public");
            p.Schema("admin");
        });

        model.Schemas.Count.ShouldBe(2);
        model.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    [Fact]
    public async Task PreDeploymentScript_AddsScriptToModel()
    {
        var model = await Build(p => p.PreDeploymentScript("init", "SELECT 1"));

        model.PreDeploymentScripts.Count.ShouldBe(1);
        model.PreDeploymentScripts[0].Name.ShouldBe("init");
        model.PreDeploymentScripts[0].Sql.ShouldBe("SELECT 1");
    }

    [Fact]
    public async Task PostDeploymentScript_AddsScriptToModel()
    {
        var model = await Build(p => p.PostDeploymentScript("seed", "INSERT INTO config DEFAULT VALUES"));

        model.PostDeploymentScripts.Count.ShouldBe(1);
        model.PostDeploymentScripts[0].Name.ShouldBe("seed");
    }

    [Fact]
    public async Task Build_WithNoScripts_ScriptListsAreEmpty()
    {
        var model = await Build(_ => { });

        model.PreDeploymentScripts.ShouldBeEmpty();
        model.PostDeploymentScripts.ShouldBeEmpty();
    }

    // ── SchemaBuilder ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SchemaBuilder_Table_AddsTableToSchema()
    {
        var model = await Build(p => p.Schema("public").Table("users"));

        model.Schemas[0].Tables.Count.ShouldBe(1);
        model.Schemas[0].Tables[0].Name.ShouldBe("users");
    }

    [Fact]
    public async Task SchemaBuilder_MultipleTables_AllAppearInSchema()
    {
        var model = await Build(p =>
        {
            var schema = p.Schema("public");
            schema.Table("users");
            schema.Table("posts");
        });

        model.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public async Task SchemaBuilder_WasPreviouslyNamed_SetsPreviousNameOnSchema()
    {
        var model = await Build(p => p.Schema("public").WasPreviouslyNamed("old_schema"));

        model.Schemas[0].PreviousName.ShouldBe("old_schema");
    }

    // ── TableBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TableBuilder_Column_AddsColumnToTable()
    {
        var model = await Build(p =>
        {
            var table = p.Schema("public").Table("users");
            table.Column("id", SqlType.BigInt);
        });

        var table = model.Schemas[0].Tables[0];
        table.Columns.Count.ShouldBe(1);
        table.Columns[0].Name.ShouldBe("id");
        table.Columns[0].Type.ShouldBe(SqlType.BigInt);
    }

    [Fact]
    public async Task TableBuilder_PrimaryKey_AttachesPrimaryKeyToTable()
    {
        var model = await Build(p =>
        {
            var table = p.Schema("public").Table("users");
            table.Column("id", SqlType.BigInt).NotNull();
            table.PrimaryKey("pk_users", ["id"]);
        });

        var table = model.Schemas[0].Tables[0];
        table.PrimaryKey.ShouldNotBeNull();
        table.PrimaryKey!.Name.ShouldBe("pk_users");
        table.PrimaryKey.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public async Task TableBuilder_ForeignKey_AddsForeignKeyToTable()
    {
        var model = await Build(p =>
        {
            var table = p.Schema("public").Table("posts");
            table.Column("user_id", SqlType.BigInt);
            table.ForeignKey("fk_posts_user", ["user_id"], "public", "users", ["id"]);
        });

        var table = model.Schemas[0].Tables[0];
        table.ForeignKeys.ShouldNotBeNull();
        table.ForeignKeys!.Count.ShouldBe(1);
        table.ForeignKeys[0].Name.ShouldBe("fk_posts_user");
        table.ForeignKeys[0].ColumnNames.ShouldBe(["user_id"]);
        table.ForeignKeys[0].ReferencedSchema.ShouldBe("public");
        table.ForeignKeys[0].ReferencedTable.ShouldBe("users");
        table.ForeignKeys[0].ReferencedColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public async Task TableBuilder_Index_AddsIndexToTable()
    {
        var model = await Build(p =>
        {
            var table = p.Schema("public").Table("users");
            table.Column("email", SqlType.Text);
            table.Index("idx_users_email", ["email"]);
        });

        var table = model.Schemas[0].Tables[0];
        table.Indexes.ShouldNotBeNull();
        table.Indexes!.Count.ShouldBe(1);
        table.Indexes[0].Name.ShouldBe("idx_users_email");
        table.Indexes[0].ColumnNames.ShouldBe(["email"]);
        table.Indexes[0].IsUnique.ShouldBeFalse();
    }

    [Fact]
    public async Task TableBuilder_WasPreviouslyNamed_SetsPreviousNameOnTable()
    {
        var model = await Build(p => p.Schema("public").Table("users").WasPreviouslyNamed("members"));

        model.Schemas[0].Tables[0].PreviousName.ShouldBe("members");
    }

    [Fact]
    public async Task TableBuilder_NoForeignKeys_ForeignKeysIsNull()
    {
        var model = await Build(p =>
        {
            var table = p.Schema("public").Table("users");
            table.Column("id", SqlType.BigInt);
        });

        model.Schemas[0].Tables[0].ForeignKeys.ShouldBeNull();
    }

    [Fact]
    public async Task TableBuilder_NoIndexes_IndexesIsNull()
    {
        var model = await Build(p =>
        {
            var table = p.Schema("public").Table("users");
            table.Column("id", SqlType.BigInt);
        });

        model.Schemas[0].Tables[0].Indexes.ShouldBeNull();
    }

    // ── ColumnBuilder ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ColumnBuilder_Defaults_IsNullableWithNoIdentityOrDefault()
    {
        var model = await Build(p => p.Schema("public").Table("t").Column("col", SqlType.Text));

        var column = model.Schemas[0].Tables[0].Columns[0];
        column.IsNullable.ShouldBeTrue();
        column.IsIdentity.ShouldBeFalse();
        column.DefaultExpression.ShouldBeNull();
        column.PreviousName.ShouldBeNull();
    }

    [Fact]
    public async Task ColumnBuilder_NotNull_SetsIsNullableFalse()
    {
        var model = await Build(p => p.Schema("public").Table("t").Column("col", SqlType.Text).NotNull());

        model.Schemas[0].Tables[0].Columns[0].IsNullable.ShouldBeFalse();
    }

    [Fact]
    public async Task ColumnBuilder_Nullable_SetsIsNullableTrue()
    {
        var model = await Build(p => p.Schema("public").Table("t").Column("col", SqlType.Text).NotNull().Nullable());

        model.Schemas[0].Tables[0].Columns[0].IsNullable.ShouldBeTrue();
    }

    [Fact]
    public async Task ColumnBuilder_Identity_SetsIsIdentityTrue()
    {
        var model = await Build(p => p.Schema("public").Table("t").Column("id", SqlType.BigInt).Identity());

        model.Schemas[0].Tables[0].Columns[0].IsIdentity.ShouldBeTrue();
    }

    [Fact]
    public async Task ColumnBuilder_Default_SetsDefaultExpression()
    {
        var model = await Build(p => p.Schema("public").Table("t").Column("created_at", SqlType.DateTimeOffset).Default("now()"));

        model.Schemas[0].Tables[0].Columns[0].DefaultExpression.ShouldBe("now()");
    }

    [Fact]
    public async Task ColumnBuilder_WasPreviouslyNamed_SetsPreviousName()
    {
        var model = await Build(p => p.Schema("public").Table("t").Column("full_name", SqlType.Text).WasPreviouslyNamed("name"));

        model.Schemas[0].Tables[0].Columns[0].PreviousName.ShouldBe("name");
    }

    // ── ForeignKeyBuilder ─────────────────────────────────────────────────────

    [Fact]
    public async Task ForeignKeyBuilder_OnDelete_SetsDeleteAction()
    {
        var model = await Build(p =>
        {
            p.Schema("public").Table("posts")
                .ForeignKey("fk", ["user_id"], "public", "users", ["id"])
                .OnDelete(ReferentialAction.Cascade);
        });

        model.Schemas[0].Tables[0].ForeignKeys![0].OnDelete.ShouldBe(ReferentialAction.Cascade);
    }

    [Fact]
    public async Task ForeignKeyBuilder_OnUpdate_SetsUpdateAction()
    {
        var model = await Build(p =>
        {
            p.Schema("public").Table("posts")
                .ForeignKey("fk", ["user_id"], "public", "users", ["id"])
                .OnUpdate(ReferentialAction.SetNull);
        });

        model.Schemas[0].Tables[0].ForeignKeys![0].OnUpdate.ShouldBe(ReferentialAction.SetNull);
    }

    [Fact]
    public async Task ForeignKeyBuilder_Defaults_NoActionForBothRules()
    {
        var model = await Build(p =>
        {
            p.Schema("public").Table("posts")
                .ForeignKey("fk", ["user_id"], "public", "users", ["id"]);
        });

        var fk = model.Schemas[0].Tables[0].ForeignKeys![0];
        fk.OnDelete.ShouldBe(ReferentialAction.NoAction);
        fk.OnUpdate.ShouldBe(ReferentialAction.NoAction);
    }

    // ── IndexBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IndexBuilder_Unique_SetsIsUniqueTrue()
    {
        var model = await Build(p =>
        {
            p.Schema("public").Table("users")
                .Index("idx_email", ["email"])
                .Unique();
        });

        model.Schemas[0].Tables[0].Indexes![0].IsUnique.ShouldBeTrue();
    }

    [Fact]
    public async Task IndexBuilder_CompositeIndex_PreservesColumnOrder()
    {
        var model = await Build(p =>
        {
            p.Schema("public").Table("t")
                .Index("idx_composite", ["last_name", "first_name"]);
        });

        model.Schemas[0].Tables[0].Indexes![0].ColumnNames.ShouldBe(["last_name", "first_name"]);
    }
}
