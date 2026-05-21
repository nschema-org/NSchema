using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSchema.Domain.Migration;
using NSchema.Domain.Migration.Actions;
using NSchema.Domain.Schema;
using NSchema.Migration;
using NSchema.Postgres.Migration;
using NSchema.Postgres.Tests.Fixtures;

namespace NSchema.Postgres.Tests.Migration;

[Collection("postgres")]
public sealed class PostgresSchemaMigratorTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private readonly NpgsqlDataSource _dataSource = fixture.DataSource;
    private readonly string _schema = $"test_{Guid.NewGuid():N}";
    private NpgsqlConnection _conn = null!;
    private PostgresSchemaMigrator _executor = null!;

    public async Task InitializeAsync()
    {
        _conn = await _dataSource.OpenConnectionAsync();
        _executor = new PostgresSchemaMigrator(NullLogger<PostgresSchemaMigrator>.Instance, _dataSource);
        await Exec($"""CREATE SCHEMA "{_schema}" """);
    }

    public async Task DisposeAsync()
    {
        await Exec($"""DROP SCHEMA IF EXISTS "{_schema}" CASCADE""");
        await _conn.DisposeAsync();
    }

    // ── Schema operations ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSchema_CreatesSchemaInDatabase()
    {
        // Arrange
        var name = $"test_{Guid.NewGuid():N}";

        // Act
        await _executor.Migrate(new MigrationPlan([new CreateSchema(name)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.schemata WHERE schema_name = '{name}'");
        exists.ShouldBeTrue();

        await Exec($"""DROP SCHEMA "{name}" CASCADE""");
    }

    [Fact]
    public async Task DropSchema_RemovesSchemaFromDatabase()
    {
        // Arrange
        var name = $"test_{Guid.NewGuid():N}";
        await Exec($"""CREATE SCHEMA "{name}" """);

        // Act
        await _executor.Migrate(new MigrationPlan([new DropSchema(name)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.schemata WHERE schema_name = '{name}'");
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RenameSchema_RenamesSchemaInDatabase()
    {
        // Arrange
        var oldName = $"test_{Guid.NewGuid():N}";
        var newName = $"test_{Guid.NewGuid():N}";
        await Exec($"""CREATE SCHEMA "{oldName}" """);

        // Act
        await _executor.Migrate(new MigrationPlan([new RenameSchema(oldName, newName)]), Allow);

        // Assert
        var oldExists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.schemata WHERE schema_name = '{oldName}'");
        var newExists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.schemata WHERE schema_name = '{newName}'");
        oldExists.ShouldBeFalse();
        newExists.ShouldBeTrue();

        await Exec($"""DROP SCHEMA "{newName}" CASCADE""");
    }

    // ── Table operations ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTable_CreatesTableInDatabase()
    {
        // Arrange
        var table = new Table("users",
            [new Column("id", SqlType.BigInt, IsNullable: false)]);

        // Act
        await _executor.Migrate(new MigrationPlan([new CreateTable(_schema, table)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'users'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTable_WithPrimaryKey_CreatesPrimaryKeyConstraint()
    {
        // Arrange
        var table = new Table("orders",
            [new Column("id", SqlType.BigInt, IsNullable: false)],
            PrimaryKey: new PrimaryKey("pk_orders", ["id"]));

        // Act
        await _executor.Migrate(new MigrationPlan([new CreateTable(_schema, table)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.table_constraints WHERE table_schema = '{_schema}' AND table_name = 'orders' AND constraint_type = 'PRIMARY KEY' AND constraint_name = 'pk_orders'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DropTable_RemovesTableFromDatabase()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."products" (id integer)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new DropTable(_schema, "products")]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'products'");
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RenameTable_RenamesTableInDatabase()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."old_name" (id integer)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new RenameTable(_schema, "old_name", "new_name")]), Allow);

        // Assert
        var oldExists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'old_name'");
        var newExists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'new_name'");
        oldExists.ShouldBeFalse();
        newExists.ShouldBeTrue();
    }

    // ── Column operations ─────────────────────────────────────────────────────

    [Fact]
    public async Task AddColumn_AddsColumnToTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer)""");
        var column = new Column("name", SqlType.VarChar(100), IsNullable: false);

        // Act
        await _executor.Migrate(new MigrationPlan([new AddColumn(_schema, "items", column)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'name'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DropColumn_RemovesColumnFromTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, name text)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new DropColumn(_schema, "items", "name")]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'name'");
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task RenameColumn_RenamesColumnInTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, old_col text)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new RenameColumn(_schema, "items", "old_col", "new_col")]), Allow);

        // Assert
        var oldExists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'old_col'");
        var newExists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'new_col'");
        oldExists.ShouldBeFalse();
        newExists.ShouldBeTrue();
    }

    [Fact]
    public async Task AlterColumnType_ChangesColumnDataType()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, value integer)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new AlterColumnType(_schema, "items", "value", SqlType.Int, SqlType.BigInt)]), Allow);

        // Assert
        var dataType = await ScalarString(
            $"SELECT data_type FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'value'");
        dataType.ShouldBe("bigint");
    }

    [Fact]
    public async Task AlterColumnNullability_MakesColumnNotNull()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, name text)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new AlterColumnNullability(_schema, "items", "name", WasNullable: true, IsNullable: false)]), Allow);

        // Assert
        var isNullable = await ScalarString(
            $"SELECT is_nullable FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'name'");
        isNullable.ShouldBe("NO");
    }

    [Fact]
    public async Task AlterColumnNullability_MakesColumnNullable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, name text NOT NULL)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new AlterColumnNullability(_schema, "items", "name", WasNullable: false, IsNullable: true)]), Allow);

        // Assert
        var isNullable = await ScalarString(
            $"SELECT is_nullable FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'name'");
        isNullable.ShouldBe("YES");
    }

    [Fact]
    public async Task SetColumnDefault_SetsDefaultExpression()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, quantity integer)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new SetColumnDefault(_schema, "items", "quantity", null, "0")]), Allow);

        // Assert
        var hasDefault = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'quantity' AND column_default IS NOT NULL");
        hasDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task SetColumnDefault_DropsDefaultExpression()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, quantity integer DEFAULT 0)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new SetColumnDefault(_schema, "items", "quantity", "0", null)]), Allow);

        // Assert
        var hasDefault = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.columns WHERE table_schema = '{_schema}' AND table_name = 'items' AND column_name = 'quantity' AND column_default IS NOT NULL");
        hasDefault.ShouldBeFalse();
    }

    // ── Primary key operations ────────────────────────────────────────────────

    [Fact]
    public async Task AddPrimaryKey_AddsConstraintToTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer NOT NULL)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new AddPrimaryKey(_schema, "items", new PrimaryKey("pk_items", ["id"]))]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.table_constraints WHERE table_schema = '{_schema}' AND table_name = 'items' AND constraint_type = 'PRIMARY KEY' AND constraint_name = 'pk_items'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DropPrimaryKey_RemovesConstraintFromTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer NOT NULL, CONSTRAINT pk_items PRIMARY KEY (id))""");

        // Act
        await _executor.Migrate(new MigrationPlan([new DropPrimaryKey(_schema, "items", "pk_items")]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.table_constraints WHERE table_schema = '{_schema}' AND table_name = 'items' AND constraint_type = 'PRIMARY KEY'");
        exists.ShouldBeFalse();
    }

    // ── Foreign key operations ────────────────────────────────────────────────

    [Fact]
    public async Task AddForeignKey_AddsReferentialConstraint()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."parents" (id integer NOT NULL, CONSTRAINT pk_parents PRIMARY KEY (id))""");
        await Exec($"""CREATE TABLE "{_schema}"."children" (id integer NOT NULL, parent_id integer)""");
        var fk = new ForeignKey("fk_children_parent", ["parent_id"], _schema, "parents", ["id"]);

        // Act
        await _executor.Migrate(new MigrationPlan([new AddForeignKey(_schema, "children", fk)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.referential_constraints WHERE constraint_schema = '{_schema}' AND constraint_name = 'fk_children_parent'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DropForeignKey_RemovesReferentialConstraint()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."parents" (id integer NOT NULL, CONSTRAINT pk_parents PRIMARY KEY (id))""");
        await Exec($"""CREATE TABLE "{_schema}"."children" (id integer, parent_id integer, CONSTRAINT fk_children_parent FOREIGN KEY (parent_id) REFERENCES "{_schema}"."parents" (id))""");

        // Act
        await _executor.Migrate(new MigrationPlan([new DropForeignKey(_schema, "children", "fk_children_parent")]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.referential_constraints WHERE constraint_schema = '{_schema}' AND constraint_name = 'fk_children_parent'");
        exists.ShouldBeFalse();
    }

    // ── Index operations ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateIndex_CreatesIndexOnTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, name text)""");
        var index = new TableIndex("idx_items_name", ["name"]);

        // Act
        await _executor.Migrate(new MigrationPlan([new CreateIndex(_schema, "items", index)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM pg_indexes WHERE schemaname = '{_schema}' AND tablename = 'items' AND indexname = 'idx_items_name'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateIndex_Unique_CreatesUniqueIndexOnTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, code text)""");
        var index = new TableIndex("idx_items_code_unique", ["code"], IsUnique: true);

        // Act
        await _executor.Migrate(new MigrationPlan([new CreateIndex(_schema, "items", index)]), Allow);

        // Assert
        var isUnique = await ScalarBool(
            $"SELECT ix.indisunique FROM pg_indexes pi JOIN pg_class t ON t.relname = pi.tablename JOIN pg_index ix ON ix.indexrelid = (SELECT oid FROM pg_class WHERE relname = 'idx_items_code_unique') WHERE pi.schemaname = '{_schema}' AND pi.indexname = 'idx_items_code_unique'");
        isUnique.ShouldBeTrue();
    }

    [Fact]
    public async Task DropIndex_RemovesIndexFromTable()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."items" (id integer, name text)""");
        await Exec($"""CREATE INDEX "idx_items_name" ON "{_schema}"."items" (name)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new DropIndex(_schema, "items", "idx_items_name")]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM pg_indexes WHERE schemaname = '{_schema}' AND indexname = 'idx_items_name'");
        exists.ShouldBeFalse();
    }

    // ── Deployment scripts ────────────────────────────────────────────────────

    [Fact]
    public async Task RunPreDeploymentScript_ExecutesSql()
    {
        // Arrange
        var script = new Script("seed", $"""CREATE TABLE "{_schema}"."seeded" (id integer)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new RunPreDeploymentScript(script)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'seeded'");
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task RunPostDeploymentScript_ExecutesSql()
    {
        // Arrange
        var script = new Script("seed", $"""CREATE TABLE "{_schema}"."seeded_post" (id integer)""");

        // Act
        await _executor.Migrate(new MigrationPlan([new RunPostDeploymentScript(script)]), Allow);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'seeded_post'");
        exists.ShouldBeTrue();
    }

    // ── Destructive action policy ─────────────────────────────────────────────

    [Fact]
    public async Task Execute_WhenPolicyIsError_ThrowsOnDestructiveAction()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."to_drop" (id integer)""");
        var options = new MigrationOptions() { DestructiveActionPolicy = DestructiveActionPolicy.Error };

        // Act
        var act = () => _executor.Migrate(new MigrationPlan([new DropTable(_schema, "to_drop")]), options);

        // Assert
        var ex = await act.ShouldThrowAsync<DestructiveActionException>();
        ex.Action.ShouldBeOfType<DropTable>();
    }

    [Fact]
    public async Task Execute_WhenPolicyIsAllow_ExecutesDestructiveAction()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."to_drop" (id integer)""");
        var options = new MigrationOptions() { DestructiveActionPolicy = DestructiveActionPolicy.Allow };

        // Act
        await _executor.Migrate(new MigrationPlan([new DropTable(_schema, "to_drop")]), options);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'to_drop'");
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_WhenPolicyIsWarn_ExecutesDestructiveAction()
    {
        // Arrange
        await Exec($"""CREATE TABLE "{_schema}"."to_drop" (id integer)""");
        var options = new MigrationOptions() { DestructiveActionPolicy = DestructiveActionPolicy.Warn };

        // Act
        await _executor.Migrate(new MigrationPlan([new DropTable(_schema, "to_drop")]), options);

        // Assert
        var exists = await ScalarBool(
            $"SELECT COUNT(*) > 0 FROM information_schema.tables WHERE table_schema = '{_schema}' AND table_name = 'to_drop'");
        exists.ShouldBeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly MigrationOptions Allow = new() { DestructiveActionPolicy = DestructiveActionPolicy.Allow };

    private async Task Exec(string sql)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<bool> ScalarBool(string sql)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<string> ScalarString(string sql)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        return (string)(await cmd.ExecuteScalarAsync())!;
    }
}
