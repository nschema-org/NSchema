using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class DefaultSchemaComparerTests
{
    private readonly DefaultSchemaComparer _comparer = new(NullLogger<DefaultSchemaComparer>.Instance);

    private static DatabaseSchema Empty() => new([]);

    private static DatabaseSchema WithSchema(string name, params Table[] tables) =>
        new([new SchemaDefinition(name, Tables: tables)]);

    private static Table SimpleTable(string name, params Column[] columns) =>
        new(name, Columns: columns.Length > 0 ? columns : [new Column("id", SqlType.Int)]);

    // ── No changes ───────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IdenticalModels_ProducesNoActions()
    {
        // Arrange
        var model = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _comparer.Compare(model, model);

        // Assert
        result.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Diff_BothEmpty_ProducesNoActions()
    {
        // Arrange
        var current = Empty();
        var desired = Empty();

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.IsEmpty.ShouldBeTrue();
    }

    // ── Schemas ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_NewSchema_ProducesCreateSchema()
    {
        // Arrange
        var current = Empty();
        var desired = WithSchema("app");

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is CreateSchema { SchemaName: "app" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedSchema_ProducesDropSchema()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = Empty();

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropSchema { SchemaName: "app" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedSchema_ProducesRenameSchema()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = new DatabaseSchema([new SchemaDefinition("application", PreviousName: "app")]);

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RenameSchema { OldName: "app", NewName: "application" }).ShouldBeTrue();
        result.Actions.Any(i => i is CreateSchema).ShouldBeFalse();
        result.Actions.Any(i => i is DropSchema).ShouldBeFalse();
    }

    // ── Tables ───────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_NewTable_ProducesCreateTable()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is CreateTable { SchemaName: "app", Table.Name: "users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedTable_ProducesDropTable()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app");

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropTable { SchemaName: "app", TableName: "users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedTable_ProducesRenameTable()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("accounts", PreviousName: "users", Columns: [new Column("id", SqlType.Int)]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RenameTable { SchemaName: "app", OldName: "users", NewName: "accounts" }).ShouldBeTrue();
        result.Actions.Any(i => i is CreateTable).ShouldBeFalse();
        result.Actions.Any(i => i is DropTable).ShouldBeFalse();
    }

    // ── Columns ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_NewColumn_ProducesAddColumn()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", Columns: [
            new Column("id", SqlType.Int),
            new Column("email", SqlType.Text)
        ]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AddColumn { TableName: "users", Column.Name: "email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedColumn_ProducesDropColumn()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [
            new Column("id", SqlType.Int),
            new Column("email", SqlType.Text)
        ]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropColumn { TableName: "users", ColumnName: "email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedColumn_ProducesRenameColumn()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [new Column("email", SqlType.Text)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("email_address", SqlType.Text, PreviousName: "email")]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RenameColumn { TableName: "users", OldName: "email", NewName: "email_address" }).ShouldBeTrue();
        result.Actions.Any(i => i is AddColumn).ShouldBeFalse();
        result.Actions.Any(i => i is DropColumn).ShouldBeFalse();
    }

    [Fact]
    public void Diff_ColumnTypeChanged_ProducesAlterColumnType()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.BigInt)]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AlterColumnType act
            && act.TableName == "users"
            && act.ColumnName == "id"
            && act.OldType == SqlType.Int
            && act.NewType == SqlType.BigInt).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ColumnNullabilityChanged_ProducesAlterColumnNullability()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [new Column("email", SqlType.Text, IsNullable: true)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("email", SqlType.Text, IsNullable: false)]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AlterColumnNullability acn
            && acn.TableName == "users"
            && acn.ColumnName == "email"
            && acn.OldNullable == true
            && acn.NewNullable == false).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ColumnDefaultChanged_ProducesSetColumnDefault()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [new Column("status", SqlType.Text)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("status", SqlType.Text, DefaultExpression: "'active'")]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetColumnDefault { TableName: "users", ColumnName: "status", OldDefault: null, NewDefault: "'active'" }).ShouldBeTrue();
    }

    // ── Primary Key ──────────────────────────────────────────────────────────

    [Fact]
    public void Diff_PrimaryKeyAdded_ProducesAddPrimaryKey()
    {
        // Arrange
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users",
            PrimaryKey: new PrimaryKey("pk_users", ["id"]), Columns: [new Column("id", SqlType.Int)]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AddPrimaryKey { TableName: "users", PrimaryKey.Name: "pk_users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PrimaryKeyDropped_ProducesDropPrimaryKey()
    {
        // Arrange
        var current = WithSchema("app", new Table("users",
            PrimaryKey: new PrimaryKey("pk_users", ["id"]), Columns: [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropPrimaryKey { TableName: "users", PrimaryKeyName: "pk_users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_UnchangedPrimaryKey_ProducesNoKeyActions()
    {
        // Arrange
        var model = WithSchema("app", new Table("users",
            PrimaryKey: new PrimaryKey("pk_users", ["id"]), Columns: [new Column("id", SqlType.Int)]));

        // Act
        var result = _comparer.Compare(model, model);

        // Assert
        result.Actions.Any(i => i is AddPrimaryKey or DropPrimaryKey).ShouldBeFalse();
    }

    // ── Foreign Keys ─────────────────────────────────────────────────────────

    [Fact]
    public void Diff_ForeignKeyAdded_ProducesAddForeignKey()
    {
        // Arrange
        var fk = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("users",
            Columns: [new Column("id", SqlType.Int)],
            ForeignKeys: [fk]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AddForeignKey { ForeignKey.Name: "fk_users_org" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ForeignKeyDropped_ProducesDropForeignKey()
    {
        // Arrange
        var fk = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", new Table("users",
            Columns: [new Column("id", SqlType.Int)],
            ForeignKeys: [fk]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropForeignKey { ForeignKeyName: "fk_users_org" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ForeignKeyModified_ProducesDropThenAdd()
    {
        // Arrange
        var original = new ForeignKey("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var modified = original with { OnDelete = ReferentialAction.Cascade };
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], ForeignKeys: [original]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], ForeignKeys: [modified]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropForeignKey { ForeignKeyName: "fk_users_org" }).ShouldBeTrue();
        result.Actions.Any(i => i is AddForeignKey { ForeignKey.Name: "fk_users_org" }).ShouldBeTrue();
    }

    // ── Indexes ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IndexAdded_ProducesCreateIndex()
    {
        // Arrange
        var idx = new TableIndex("ix_users_email", ["email"]);
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], Indexes: [idx]));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is CreateIndex { Index.Name: "ix_users_email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_IndexDropped_ProducesDropIndex()
    {
        // Arrange
        var idx = new TableIndex("ix_users_email", ["email"]);
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], Indexes: [idx]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _comparer.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropIndex { IndexName: "ix_users_email" }).ShouldBeTrue();
    }

    // ── Partial schemas ──────────────────────────────────────────────────────

    [Fact]
    public void Diff_PartialSchema_DoesNotDropUnmanagedTables()
    {
        var current = WithSchema("app", SimpleTable("users"), SimpleTable("legacy"));
        var desired = new DatabaseSchema([new SchemaDefinition("app", IsPartial: true, Tables: [SimpleTable("users")])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is DropTable { TableName: "legacy" }).ShouldBeFalse();
    }

    [Fact]
    public void Diff_PartialSchema_StillManagesDeclaredTables()
    {
        var current = WithSchema("app", SimpleTable("users"), SimpleTable("legacy"));
        var desired = new DatabaseSchema([new SchemaDefinition("app", IsPartial: true, Tables: [SimpleTable("users"), SimpleTable("orders")])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is CreateTable { Table.Name: "orders" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PartialSchema_ExplicitDropTable_DropsSpecifiedTable()
    {
        var current = WithSchema("app", SimpleTable("users"), SimpleTable("legacy"));
        var desired = new DatabaseSchema([new SchemaDefinition("app", IsPartial: true, Tables: [SimpleTable("users")], DroppedTables: ["legacy"])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is DropTable { TableName: "legacy" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PartialSchema_ExplicitDropTable_NotInCurrent_ProducesNoAction()
    {
        var current = WithSchema("app", SimpleTable("users"));
        var desired = new DatabaseSchema([new SchemaDefinition("app", IsPartial: true, Tables: [SimpleTable("users")], DroppedTables: ["nonexistent"])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is DropTable { TableName: "nonexistent" }).ShouldBeFalse();
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_SchemaCommentAdded_ProducesSetSchemaComment()
    {
        var current = new DatabaseSchema([new SchemaDefinition("app")]);
        var desired = new DatabaseSchema([new SchemaDefinition("app", Comment: "Application schema")]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetSchemaComment { SchemaName: "app", OldComment: null, NewComment: "Application schema" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_SchemaCommentRemoved_ProducesSetSchemaComment()
    {
        var current = new DatabaseSchema([new SchemaDefinition("app", Comment: "Old comment")]);
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetSchemaComment { SchemaName: "app", OldComment: "Old comment", NewComment: null }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewSchemaWithComment_ProducesSetSchemaComment()
    {
        var current = Empty();
        var desired = new DatabaseSchema([new SchemaDefinition("app", Comment: "Application schema", Tables: [])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetSchemaComment { SchemaName: "app", NewComment: "Application schema" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_TableCommentAdded_ProducesSetTableComment()
    {
        var current = WithSchema("app", SimpleTable("users"));
        var desired = new DatabaseSchema([new SchemaDefinition("app", Tables: [new Table("users", Comment: "User accounts", Columns: [new Column("id", SqlType.Int)])])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetTableComment { TableName: "users", OldComment: null, NewComment: "User accounts" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewTableWithComment_ProducesSetTableComment()
    {
        var current = WithSchema("app");
        var desired = new DatabaseSchema([new SchemaDefinition("app", Tables: [new Table("users", Comment: "User accounts", Columns: [new Column("id", SqlType.Int)])])]);

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetTableComment { TableName: "users", NewComment: "User accounts" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ColumnCommentAdded_ProducesSetColumnComment()
    {
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int, Comment: "Primary key")]));

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetColumnComment { ColumnName: "id", OldComment: null, NewComment: "Primary key" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewColumnWithComment_ProducesSetColumnComment()
    {
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int), new Column("email", SqlType.Text, Comment: "Email address")]));

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetColumnComment { ColumnName: "email", NewComment: "Email address" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_IndexCommentAdded_ProducesSetIndexComment()
    {
        var idx = new TableIndex("ix_users_email", ["email"]);
        var idxWithComment = new TableIndex("ix_users_email", ["email"], Comment: "Email lookup index");
        var current = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], Indexes: [idx]));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], Indexes: [idxWithComment]));

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is SetIndexComment { IndexName: "ix_users_email", OldComment: null, NewComment: "Email lookup index" }).ShouldBeTrue();
        result.Actions.Any(i => i is DropIndex or CreateIndex).ShouldBeFalse();
    }

    [Fact]
    public void Diff_NewIndexWithComment_ProducesCreateIndexAndSetIndexComment()
    {
        var idx = new TableIndex("ix_users_email", ["email"], Comment: "Email lookup index");
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", new Table("users", Columns: [new Column("id", SqlType.Int)], Indexes: [idx]));

        var result = _comparer.Compare(current, desired);

        result.Actions.Any(i => i is CreateIndex { Index.Name: "ix_users_email" }).ShouldBeTrue();
        result.Actions.Any(i => i is SetIndexComment { IndexName: "ix_users_email", NewComment: "Email lookup index" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_CommentUnchanged_ProducesNoCommentActions()
    {
        var model = new DatabaseSchema([new SchemaDefinition("app",
            Comment: "App schema", Tables: [new Table("users", Comment: "Users", Columns: [new Column("id", SqlType.Int, Comment: "PK")])])]);

        var result = _comparer.Compare(model, model);

        result.Actions.Any(i => i is SetSchemaComment or SetTableComment or SetColumnComment or SetIndexComment).ShouldBeFalse();
    }

}
