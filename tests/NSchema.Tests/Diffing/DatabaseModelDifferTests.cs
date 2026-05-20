using NSchema.Diffing;
using NSchema.Domain;
using NSchema.Instructions;

namespace NSchema.Tests.Diffing;

public class DatabaseModelDifferTests
{
    private readonly DatabaseModelDiffer _differ = new();

    private static DatabaseModel Empty() => new([]);

    private static DatabaseModel WithSchema(string name, params Table[] tables) =>
        new([new DatabaseSchema(name, tables)]);

    private static Table SimpleTable(string name, params Column[] columns) =>
        new(name, columns.Length > 0 ? columns : [new Column("id", SqlType.Int)]);

    // ── No changes ───────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IdenticalModels_ProducesNoInstructions()
    {
        // Arrange
        var model = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _differ.Diff(model, model);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Diff_BothEmpty_ProducesNoInstructions()
    {
        // Arrange
        var current = Empty();
        var desired = Empty();

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.ShouldBeEmpty();
    }

    // ── Schemas ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_NewSchema_ProducesCreateSchema()
    {
        // Arrange
        var current = Empty();
        var desired = WithSchema("app");

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is CreateSchema { SchemaName: "app" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedSchema_ProducesDropSchema()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = Empty();

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropSchema { SchemaName: "app" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedSchema_ProducesRenameSchema()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = new DatabaseModel([new DatabaseSchema("application", [], PreviousName: "app")]);

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is RenameSchema { OldName: "app", NewName: "application" }).ShouldBeTrue();
        result.Any(i => i is CreateSchema).ShouldBeFalse();
        result.Any(i => i is DropSchema).ShouldBeFalse();
    }

    // ── Tables ───────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_NewTable_ProducesCreateTable()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is CreateTable { SchemaName: "app", Table.Name: "users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedTable_ProducesDropTable()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app");

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropTable { SchemaName: "app", TableName: "users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedTable_ProducesRenameTable()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("accounts", [new Column("id", SqlType.Int)], PreviousName: "users"));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is RenameTable { SchemaName: "app", OldName: "users", NewName: "accounts" }).ShouldBeTrue();
        result.Any(i => i is CreateTable).ShouldBeFalse();
        result.Any(i => i is DropTable).ShouldBeFalse();
    }

    // ── Columns ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_NewColumn_ProducesAddColumn()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", [
            new Column("id", SqlType.Int),
            new Column("email", SqlType.Text)
        ]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is AddColumn { TableName: "users", Column.Name: "email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedColumn_ProducesDropColumn()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [
            new Column("id", SqlType.Int),
            new Column("email", SqlType.Text)
        ]));
        var desired = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropColumn { TableName: "users", ColumnName: "email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedColumn_ProducesRenameColumn()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [new Column("email", SqlType.Text)]));
        var desired = WithSchema("app", new Table("users", [new Column("email_address", SqlType.Text, PreviousName: "email")]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is RenameColumn { TableName: "users", OldName: "email", NewName: "email_address" }).ShouldBeTrue();
        result.Any(i => i is AddColumn).ShouldBeFalse();
        result.Any(i => i is DropColumn).ShouldBeFalse();
    }

    [Fact]
    public void Diff_ColumnTypeChanged_ProducesAlterColumnType()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", [new Column("id", SqlType.BigInt)]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is AlterColumnType act
            && act.TableName == "users"
            && act.ColumnName == "id"
            && act.OldType == SqlType.Int
            && act.NewType == SqlType.BigInt).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ColumnNullabilityChanged_ProducesAlterColumnNullability()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [new Column("email", SqlType.Text, IsNullable: true)]));
        var desired = WithSchema("app", new Table("users", [new Column("email", SqlType.Text, IsNullable: false)]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is AlterColumnNullability acn
            && acn.TableName == "users"
            && acn.ColumnName == "email"
            && acn.WasNullable == true
            && acn.IsNullable == false).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ColumnDefaultChanged_ProducesSetColumnDefault()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [new Column("status", SqlType.Text)]));
        var desired = WithSchema("app", new Table("users", [new Column("status", SqlType.Text, DefaultExpression: "'active'")]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is SetColumnDefault scd
            && scd.TableName == "users"
            && scd.ColumnName == "status"
            && scd.OldDefault == null
            && scd.NewDefault == "'active'").ShouldBeTrue();
    }

    // ── Primary Key ──────────────────────────────────────────────────────────

    [Fact]
    public void Diff_PrimaryKeyAdded_ProducesAddPrimaryKey()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users",
            [new Column("id", SqlType.Int)],
            PrimaryKey: new PrimaryKey("pk_users", ["id"])));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is AddPrimaryKey { TableName: "users", PrimaryKey.Name: "pk_users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PrimaryKeyDropped_ProducesDropPrimaryKey()
    {
        // Arrange
        var current = WithSchema("app", new Table("users",
            [new Column("id", SqlType.Int)],
            PrimaryKey: new PrimaryKey("pk_users", ["id"])));
        var desired = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropPrimaryKey { TableName: "users", PrimaryKeyName: "pk_users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_UnchangedPrimaryKey_ProducesNoKeyInstructions()
    {
        // Arrange
        var model = WithSchema("app", new Table("users",
            [new Column("id", SqlType.Int)],
            PrimaryKey: new PrimaryKey("pk_users", ["id"])));

        // Act
        var result = _differ.Diff(model, model);

        // Assert
        result.Any(i => i is AddPrimaryKey or DropPrimaryKey).ShouldBeFalse();
    }

    // ── Foreign Keys ─────────────────────────────────────────────────────────

    [Fact]
    public void Diff_ForeignKeyAdded_ProducesAddForeignKey()
    {
        // Arrange
        var fk = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("users",
            [new Column("id", SqlType.Int)],
            ForeignKeys: [fk]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is AddForeignKey { ForeignKey.Name: "fk_users_org" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ForeignKeyDropped_ProducesDropForeignKey()
    {
        // Arrange
        var fk = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", new Table("users",
            [new Column("id", SqlType.Int)],
            ForeignKeys: [fk]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropForeignKey { ForeignKeyName: "fk_users_org" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ForeignKeyModified_ProducesDropThenAdd()
    {
        // Arrange
        var original = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var modified = original with { OnDelete = ReferentialAction.Cascade };
        var current = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)], ForeignKeys: [original]));
        var desired = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)], ForeignKeys: [modified]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropForeignKey { ForeignKeyName: "fk_users_org" }).ShouldBeTrue();
        result.Any(i => i is AddForeignKey { ForeignKey.Name: "fk_users_org" }).ShouldBeTrue();
    }

    // ── Indexes ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IndexAdded_ProducesCreateIndex()
    {
        // Arrange
        var idx = new TableIndex("ix_users_email", ["email"]);
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)], Indexes: [idx]));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is CreateIndex { Index.Name: "ix_users_email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_IndexDropped_ProducesDropIndex()
    {
        // Arrange
        var idx = new TableIndex("ix_users_email", ["email"]);
        var current = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)], Indexes: [idx]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _differ.Diff(current, desired);

        // Assert
        result.Any(i => i is DropIndex { IndexName: "ix_users_email" }).ShouldBeTrue();
    }

    // ── Deployment scripts ───────────────────────────────────────────────────

    [Fact]
    public void Diff_PreDeploymentScript_IsFirstInstruction()
    {
        // Arrange
        var script = new DeploymentScript("install_citext", "CREATE EXTENSION IF NOT EXISTS citext;");
        var desired = new DatabaseModel([], PreDeploymentScripts: [script]);

        // Act
        var result = _differ.Diff(Empty(), desired);

        // Assert
        result[0].ShouldBeOfType<RunPreDeploymentScript>()
            .Script.Name.ShouldBe("install_citext");
    }

    [Fact]
    public void Diff_PostDeploymentScript_IsLastInstruction()
    {
        // Arrange
        var script = new DeploymentScript("seed", "INSERT INTO app.config VALUES ('version', '1');");
        var desired = new DatabaseModel(
            [new DatabaseSchema("app", [SimpleTable("config")])],
            PostDeploymentScripts: [script]);

        // Act
        var result = _differ.Diff(Empty(), desired);

        // Assert
        result[^1].ShouldBeOfType<RunPostDeploymentScript>()
            .Script.Name.ShouldBe("seed");
    }

    // ── Ordering ─────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_ForeignKeyDroppedBeforeColumnDrop()
    {
        // Arrange
        var fk = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", new Table("users",
            [new Column("id", SqlType.Int), new Column("org_id", SqlType.Int)],
            ForeignKeys: [fk]));
        var desired = WithSchema("app", new Table("users", [new Column("id", SqlType.Int)]));

        // Act
        var result = _differ.Diff(current, desired).ToList();

        // Assert
        result.FindIndex(i => i is DropForeignKey).ShouldBeLessThan(
            result.FindIndex(i => i is DropColumn));
    }

    [Fact]
    public void Diff_ForeignKeysAddedAfterTablesCreated()
    {
        // Arrange
        var fk = new ForeignKey("fk_orders_user", ["user_id"], "app", "users", ["id"]);
        var desired = new DatabaseModel([new DatabaseSchema("app", [
            new Table("users",  [new Column("id", SqlType.Int)]),
            new Table("orders", [new Column("id", SqlType.Int), new Column("user_id", SqlType.Int)],
                ForeignKeys: [fk]),
        ])]);

        // Act
        var result = _differ.Diff(Empty(), desired).ToList();

        // Assert
        result.FindLastIndex(i => i is CreateTable).ShouldBeLessThan(
            result.FindIndex(i => i is AddForeignKey));
    }
}
