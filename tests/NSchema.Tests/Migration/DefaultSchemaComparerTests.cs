using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class DefaultSchemaComparerTests
{
    private readonly DefaultSchemaComparer _sut = new(NullLogger<DefaultSchemaComparer>.Instance);

    private static DatabaseSchema Empty() => DatabaseSchema.Create([]);

    private static DatabaseSchema WithSchema(string name, params Table[] tables) =>
        DatabaseSchema.Create([SchemaDefinition.Create(name, tables: tables)]);

    private static Table SimpleTable(string name, params Column[] columns) =>
        Table.Create(name, columns: columns.Length > 0 ? columns : [Column.Create("id", SqlType.Int)]);

    // ── No changes ───────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IdenticalModels_ProducesNoActions()
    {
        // Arrange
        var model = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _sut.Compare(model, model);

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
        var result = _sut.Compare(current, desired);

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
        var result = _sut.Compare(current, desired);

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
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropSchema { SchemaName: "app" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedSchema_ProducesRenameSchema()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("application", oldName: "app")]);

        // Act
        var result = _sut.Compare(current, desired);

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
        var result = _sut.Compare(current, desired);

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
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropTable { SchemaName: "app", TableName: "users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedTable_ProducesRenameTable()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", Table.Create("accounts", oldName: "users", columns: [Column.Create("id", SqlType.Int)]));

        // Act
        var result = _sut.Compare(current, desired);

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
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users", columns: [
            Column.Create("id", SqlType.Int),
            Column.Create("email", SqlType.Text)
        ]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AddColumn { TableName: "users", Column.Name: "email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_DroppedColumn_ProducesDropColumn()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [
            Column.Create("id", SqlType.Int),
            Column.Create("email", SqlType.Text)
        ]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropColumn { TableName: "users", ColumnName: "email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_RenamedColumn_ProducesRenameColumn()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("email", SqlType.Text)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("email_address", SqlType.Text, oldName: "email")]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RenameColumn { TableName: "users", OldName: "email", NewName: "email_address" }).ShouldBeTrue();
        result.Actions.Any(i => i is AddColumn).ShouldBeFalse();
        result.Actions.Any(i => i is DropColumn).ShouldBeFalse();
    }

    [Fact]
    public void Diff_ColumnTypeChanged_ProducesAlterColumnType()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.BigInt)]));

        // Act
        var result = _sut.Compare(current, desired);

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
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("email", SqlType.Text, isNullable: true)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("email", SqlType.Text, isNullable: false)]));

        // Act
        var result = _sut.Compare(current, desired);

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
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("status", SqlType.Text)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("status", SqlType.Text, defaultExpression: "'active'")]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetColumnDefault { TableName: "users", ColumnName: "status", OldDefault: null, NewDefault: "'active'" }).ShouldBeTrue();
    }

    // ── Primary Key ──────────────────────────────────────────────────────────

    [Fact]
    public void Diff_PrimaryKeyAdded_ProducesAddPrimaryKey()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users",
            primaryKey: new PrimaryKey("pk_users", ["id"]), columns: [Column.Create("id", SqlType.Int)]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AddPrimaryKey { TableName: "users", PrimaryKey.Name: "pk_users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PrimaryKeyDropped_ProducesDropPrimaryKey()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users",
            primaryKey: new PrimaryKey("pk_users", ["id"]), columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropPrimaryKey { TableName: "users", PrimaryKeyName: "pk_users" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_UnchangedPrimaryKey_ProducesNoKeyActions()
    {
        // Arrange
        var model = WithSchema("app", Table.Create("users",
            primaryKey: new PrimaryKey("pk_users", ["id"]), columns: [Column.Create("id", SqlType.Int)]));

        // Act
        var result = _sut.Compare(model, model);

        // Assert
        result.Actions.Any(i => i is AddPrimaryKey or DropPrimaryKey).ShouldBeFalse();
    }

    // ── Foreign Keys ─────────────────────────────────────────────────────────

    [Fact]
    public void Diff_ForeignKeyAdded_ProducesAddForeignKey()
    {
        // Arrange
        var fk = ForeignKey.Create("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            foreignKeys: [fk]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AddForeignKey { ForeignKey.Name: "fk_users_org" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ForeignKeyDropped_ProducesDropForeignKey()
    {
        // Arrange
        var fk = ForeignKey.Create("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var current = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            foreignKeys: [fk]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropForeignKey { ForeignKeyName: "fk_users_org" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ForeignKeyModified_ProducesDropThenAdd()
    {
        // Arrange
        var original = ForeignKey.Create("fk_users_org", ["org_id"], "app", "organisations", ["id"]);
        var modified = original with { OnDelete = ReferentialAction.Cascade };
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], foreignKeys: [original]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], foreignKeys: [modified]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropForeignKey { ForeignKeyName: "fk_users_org" }).ShouldBeTrue();
        result.Actions.Any(i => i is AddForeignKey { ForeignKey.Name: "fk_users_org" }).ShouldBeTrue();
    }

    // ── Indexes ──────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IndexAdded_ProducesCreateIndex()
    {
        // Arrange
        var idx = TableIndex.Create("ix_users_email", ["email"]);
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], indexes: [idx]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is CreateIndex { Index.Name: "ix_users_email" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_IndexDropped_ProducesDropIndex()
    {
        // Arrange
        var idx = TableIndex.Create("ix_users_email", ["email"]);
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], indexes: [idx]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropIndex { IndexName: "ix_users_email" }).ShouldBeTrue();
    }

    // ── Partial schemas ──────────────────────────────────────────────────────

    [Fact]
    public void Diff_PartialSchema_DoesNotDropUnmanagedTables()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"), SimpleTable("legacy"));
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", isPartial: true, tables: [SimpleTable("users")])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropTable { TableName: "legacy" }).ShouldBeFalse();
    }

    [Fact]
    public void Diff_PartialSchema_StillManagesDeclaredTables()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"), SimpleTable("legacy"));
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", isPartial: true, tables: [SimpleTable("users"), SimpleTable("orders")])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is CreateTable { Table.Name: "orders" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PartialSchema_ExplicitDropTable_DropsSpecifiedTable()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"), SimpleTable("legacy"));
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", isPartial: true, tables: [SimpleTable("users")], droppedTables: ["legacy"])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropTable { TableName: "legacy" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_PartialSchema_ExplicitDropTable_NotInCurrent_ProducesNoAction()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", isPartial: true, tables: [SimpleTable("users")], droppedTables: ["nonexistent"])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is DropTable { TableName: "nonexistent" }).ShouldBeFalse();
    }

    // ── Comments ─────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_SchemaCommentAdded_ProducesSetSchemaComment()
    {
        // Arrange
        var current = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", comment: "Application schema")]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetSchemaComment { SchemaName: "app", OldComment: null, NewComment: "Application schema" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_SchemaCommentRemoved_ProducesSetSchemaComment()
    {
        // Arrange
        var current = DatabaseSchema.Create([SchemaDefinition.Create("app", comment: "Old comment")]);
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app")]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetSchemaComment { SchemaName: "app", OldComment: "Old comment", NewComment: null }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewSchemaWithComment_ProducesSetSchemaComment()
    {
        // Arrange
        var current = Empty();
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", comment: "Application schema", tables: [])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetSchemaComment { SchemaName: "app", NewComment: "Application schema" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_TableCommentAdded_ProducesSetTableComment()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [Table.Create("users", comment: "User accounts", columns: [Column.Create("id", SqlType.Int)])])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetTableComment { TableName: "users", OldComment: null, NewComment: "User accounts" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewTableWithComment_ProducesSetTableComment()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [Table.Create("users", comment: "User accounts", columns: [Column.Create("id", SqlType.Int)])])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetTableComment { TableName: "users", NewComment: "User accounts" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_ColumnCommentAdded_ProducesSetColumnComment()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int, comment: "Primary key")]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetColumnComment { ColumnName: "id", OldComment: null, NewComment: "Primary key" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewColumnWithComment_ProducesSetColumnComment()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int), Column.Create("email", SqlType.Text, comment: "Email address")]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetColumnComment { ColumnName: "email", NewComment: "Email address" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_IndexCommentAdded_ProducesSetIndexComment()
    {
        // Arrange
        var idx = TableIndex.Create("ix_users_email", ["email"]);
        var idxWithComment = TableIndex.Create("ix_users_email", ["email"], comment: "Email lookup index");
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], indexes: [idx]));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], indexes: [idxWithComment]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is SetIndexComment { IndexName: "ix_users_email", OldComment: null, NewComment: "Email lookup index" }).ShouldBeTrue();
        result.Actions.Any(i => i is DropIndex or CreateIndex).ShouldBeFalse();
    }

    [Fact]
    public void Diff_NewIndexWithComment_ProducesCreateIndexAndSetIndexComment()
    {
        // Arrange
        var idx = TableIndex.Create("ix_users_email", ["email"], comment: "Email lookup index");
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)], indexes: [idx]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is CreateIndex { Index.Name: "ix_users_email" }).ShouldBeTrue();
        result.Actions.Any(i => i is SetIndexComment { IndexName: "ix_users_email", NewComment: "Email lookup index" }).ShouldBeTrue();
    }

    // ── Schema grants ────────────────────────────────────────────────────────

    [Fact]
    public void Diff_SchemaGrantAdded_ProducesGrantSchemaUsage()
    {
        // Arrange
        var current = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", grants: [new SchemaGrant("reporting")])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is GrantSchemaUsage { SchemaName: "app", Role: "reporting" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_SchemaGrantRemoved_ProducesRevokeSchemaUsage()
    {
        // Arrange
        var current = DatabaseSchema.Create([SchemaDefinition.Create("app", grants: [new SchemaGrant("reporting")])]);
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app")]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RevokeSchemaUsage { SchemaName: "app", Role: "reporting" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_NewSchemaWithGrants_ProducesGrantSchemaUsage()
    {
        // Arrange
        var current = Empty();
        var desired = DatabaseSchema.Create([SchemaDefinition.Create("app", grants: [new SchemaGrant("app_user")])]);

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is GrantSchemaUsage { SchemaName: "app", Role: "app_user" }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_SchemaGrantsUnchanged_ProducesNoGrantActions()
    {
        // Arrange
        var model = DatabaseSchema.Create([SchemaDefinition.Create("app", grants: [new SchemaGrant("app_user")])]);

        // Act
        var result = _sut.Compare(model, model);

        // Assert
        result.Actions.Any(i => i is GrantSchemaUsage or RevokeSchemaUsage).ShouldBeFalse();
    }

    // ── Table grants ─────────────────────────────────────────────────────────

    [Fact]
    public void Diff_TableGrantAdded_ProducesGrantTablePrivileges()
    {
        // Arrange
        var current = WithSchema("app", SimpleTable("users"));
        var desired = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("reporting", TablePrivilege.Select)]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is GrantTablePrivileges
        { TableName: "users", Role: "reporting", Privileges: TablePrivilege.Select }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_TableGrantRemoved_ProducesRevokeTablePrivileges()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("reporting", TablePrivilege.Select)]));
        var desired = WithSchema("app", SimpleTable("users"));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RevokeTablePrivileges
        { TableName: "users", Role: "reporting", Privileges: TablePrivilege.Select }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_TableGrantPrivilegesChanged_ProducesRevokeThenGrant()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("app_user", TablePrivilege.Select)]));
        var desired = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("app_user", TablePrivilege.All)]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is RevokeTablePrivileges
        { Role: "app_user", Privileges: TablePrivilege.Select }).ShouldBeTrue();
        result.Actions.Any(i => i is GrantTablePrivileges
        { Role: "app_user", Privileges: TablePrivilege.All }).ShouldBeTrue();
    }

    [Fact]
    public void Diff_TableGrantUnchanged_ProducesNoGrantActions()
    {
        // Arrange
        var model = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("app_user", TablePrivilege.All)]));

        // Act
        var result = _sut.Compare(model, model);

        // Assert
        result.Actions.Any(i => i is GrantTablePrivileges or RevokeTablePrivileges).ShouldBeFalse();
    }

    [Fact]
    public void Diff_NewTableWithGrants_ProducesGrantTablePrivileges()
    {
        // Arrange
        var current = WithSchema("app");
        var desired = WithSchema("app", Table.Create("users",
            columns: [Column.Create("id", SqlType.Int)],
            grants: [new TableGrant("app_user", TablePrivilege.All)]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is GrantTablePrivileges
        { TableName: "users", Role: "app_user", Privileges: TablePrivilege.All }).ShouldBeTrue();
    }

    // ── Identity ─────────────────────────────────────────────────────────────

    [Fact]
    public void Diff_IdentityOptionsChanged_ProducesAlterIdentitySequence()
    {
        // Arrange
        var current = WithSchema("app", Table.Create("users", columns: [
            Column.Create("id", SqlType.Int, isIdentity: true,
                identityOptions: new IdentityOptions(1, 1, 1))]));
        var desired = WithSchema("app", Table.Create("users", columns: [
            Column.Create("id", SqlType.Int, isIdentity: true,
                identityOptions: new IdentityOptions(1000, 1000, 1))]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AlterIdentitySequence ais
            && ais.ColumnName == "id"
            && ais.NewOptions == new IdentityOptions(1000, 1000, 1)
            && ais.OldOptions == new IdentityOptions(1, 1, 1)).ShouldBeTrue();
    }

    [Fact]
    public void Diff_IdentityOptionsUnchanged_ProducesNoAlterIdentitySequence()
    {
        // Arrange
        var model = WithSchema("app", Table.Create("users", columns: [
            Column.Create("id", SqlType.Int, isIdentity: true,
                identityOptions: new IdentityOptions(1, 1, 1))]));

        // Act
        var result = _sut.Compare(model, model);

        // Assert
        result.Actions.Any(i => i is AlterIdentitySequence).ShouldBeFalse();
    }

    [Fact]
    public void Diff_NonIdentityToIdentity_DoesNotProduceAlterIdentitySequence()
    {
        // Arrange
        // Identity sequence is only altered when both sides are identity.
        var current = WithSchema("app", Table.Create("users", columns: [Column.Create("id", SqlType.Int)]));
        var desired = WithSchema("app", Table.Create("users", columns: [
            Column.Create("id", SqlType.Int, isIdentity: true,
                identityOptions: new IdentityOptions(1, 1, 1))]));

        // Act
        var result = _sut.Compare(current, desired);

        // Assert
        result.Actions.Any(i => i is AlterIdentitySequence).ShouldBeFalse();
    }

    [Fact]
    public void Diff_CommentUnchanged_ProducesNoCommentActions()
    {
        // Arrange
        var model = DatabaseSchema.Create([SchemaDefinition.Create("app",
            comment: "App schema", tables: [Table.Create("users", comment: "Users", columns: [Column.Create("id", SqlType.Int, comment: "PK")])])]);

        // Act
        var result = _sut.Compare(model, model);

        // Assert
        result.Actions.Any(i => i is SetSchemaComment or SetTableComment or SetColumnComment or SetIndexComment).ShouldBeFalse();
    }

}
