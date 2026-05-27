using System.Collections.Frozen;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Migration;

public class DefaultMigrationPlanRendererTests
{
    private readonly DefaultMigrationPlanRenderer _sut = new();

    // One sample per concrete MigrationAction subtype. Passing each through Render() exercises
    // the renderer's switches; any missing case hits the `_ => throw NotSupportedException`
    // default and fails the coverage test below.
    private static readonly IReadOnlyDictionary<Type, MigrationAction> _samplesByType =
        new MigrationAction[]
        {
            new CreateSchema("s"),
            new DropSchema("s"),
            new RenameSchema("s_old", "s"),
            new SetSchemaComment("s", null, "new"),
            new GrantSchemaUsage("s", "role"),
            new RevokeSchemaUsage("s", "role"),

            new CreateTable("s", Table.Create("t", columns: [Column.Create("id", SqlType.Int, isNullable: false)])),
            new DropTable("s", "t"),
            new RenameTable("s", "t_old", "t"),
            new SetTableComment("s", "t", null, "new"),
            new GrantTablePrivileges("s", "t", "role", TablePrivilege.Select),
            new RevokeTablePrivileges("s", "t", "role", TablePrivilege.Select),

            new AddColumn("s", "t", Column.Create("c", SqlType.Int)),
            new DropColumn("s", "t", "c"),
            new RenameColumn("s", "t", "c_old", "c"),
            new AlterColumnType("s", "t", "c", SqlType.Int, SqlType.BigInt),
            new AlterColumnNullability("s", "t", "c", false, true),
            new AlterIdentitySequence("s", "t", "c", null, new IdentityOptions(1, 1, 1)),
            new SetColumnDefault("s", "t", "c", null, "0"),
            new SetColumnComment("s", "t", "c", null, "new"),

            new CreateIndex("s", "t", TableIndex.Create("ix", ["c"])),
            new DropIndex("s", "t", "ix"),
            new SetIndexComment("s", "t", "ix", null, "new"),

            new AddPrimaryKey("s", "t", new PrimaryKey("pk", ["id"])),
            new DropPrimaryKey("s", "t", "pk"),
            new AddForeignKey("s", "t", ForeignKey.Create("fk", ["c"], "s2", "t2", ["id"])),
            new DropForeignKey("s", "t", "fk"),

            new RunScript(new Script("script.sql", "SELECT 1", ScriptType.PreDeployment)),
        }.ToFrozenDictionary(a => a.GetType());

    public static TheoryData<Type> AllConcreteActionTypes()
    {
        var data = new TheoryData<Type>();
        var types = typeof(MigrationAction).Assembly
            .GetTypes()
            .Where(t => t is { IsSealed: true, IsAbstract: false } && t.IsAssignableTo(typeof(MigrationAction)));
        foreach (var t in types)
        {
            data.Add(t);
        }
        return data;
    }

    [Fact]
    public void Samples_CoverEveryConcreteActionType()
    {
        // Arrange
        // Catches "added a new MigrationAction subtype but forgot to add a sample below" before
        // the per-type render test runs.
        var allActionTypes = typeof(MigrationAction).Assembly
            .GetTypes()
            .Where(t => t is { IsSealed: true, IsAbstract: false } && t.IsAssignableTo(typeof(MigrationAction)))
            .ToList();

        // Act

        // Assert
        allActionTypes.ShouldNotBeEmpty();
        foreach (var type in allActionTypes)
        {
            _samplesByType.ShouldContainKey(type, $"Missing renderer test sample for {type.Name}");
        }
    }

    [Theory]
    [MemberData(nameof(AllConcreteActionTypes))]
    public void Render_HandlesEveryConcreteActionType(Type actionType)
    {
        // Arrange
        var sample = _samplesByType[actionType];
        var plan = new MigrationPlan([sample], DatabaseSchema.Create([]));

        // Act
        // Should not throw — the switches in the renderer have `_ => throw` defaults, so a
        // missing case for any new action type will fail here.
        var act = () => _sut.Render(plan);

        // Assert
        var output = Should.NotThrow(act);
        output.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Render_EmptyPlan_ReturnsNoChangesMessage()
    {
        // Arrange

        // Act
        var output = _sut.Render(TestData.EmptyPlan);

        // Assert
        output.ShouldBe("No changes detected.");
    }

    [Fact]
    public void Render_GroupsActionsByTable_AndIncludesSummaryHeader()
    {
        // Arrange
        var plan = new MigrationPlan([
            new CreateSchema("app"),
            new AddColumn("app", "orders", Column.Create("shipped_at", SqlType.DateTimeOffset)),
            new DropTable("app", "audit_log"),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("Plan: 1 to add, 1 to change, 1 to destroy.");
        output.ShouldContain("schema app");
        output.ShouldContain("table app.orders");
        output.ShouldContain("shipped_at");
        output.ShouldContain("table app.audit_log");
    }

    [Fact]
    public void Render_CreateTable_ListsEveryColumnUnderTheHeader()
    {
        // Arrange
        var plan = new MigrationPlan([
            new CreateTable("app", Table.Create("users", columns:
            [
                Column.Create("id", SqlType.Int, isNullable: false),
                Column.Create("email", SqlType.Text, isNullable: false),
                Column.Create("created_at", SqlType.DateTimeOffset, isNullable: true),
            ])),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("+ table app.users");
        output.ShouldContain("+ id Int not null");
        output.ShouldContain("+ email Text not null");
        output.ShouldContain("+ created_at DateTimeOffset null");
    }

    [Fact]
    public void Render_CreateTable_AlsoRendersPrimaryKeyForeignKeyAndIndexLines()
    {
        // Arrange
        // Regression: previously the renderer emitted columns only for new tables and silently
        // dropped PK/FK/index/grant actions sharing the same group key.
        var plan = new MigrationPlan([
            new CreateTable("app", Table.Create("orders", columns:
            [
                Column.Create("id", SqlType.Int, isNullable: false),
                Column.Create("user_id", SqlType.Int, isNullable: false),
            ])),
            new AddPrimaryKey("app", "orders", new PrimaryKey("orders_pkey", ["id"])),
            new AddForeignKey("app", "orders", ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])),
            new CreateIndex("app", "orders", TableIndex.Create("orders_user_ix", ["user_id"])),
            new GrantTablePrivileges("app", "orders", "reader", TablePrivilege.Insert),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("+ table app.orders");
        output.ShouldContain("+ id Int not null");
        output.ShouldContain("+ user_id Int not null");
        output.ShouldContain("+ primary key orders_pkey");
        output.ShouldContain("+ foreign key orders_user_fk");
        output.ShouldContain("+ index orders_user_ix");
        output.ShouldContain("+ grant Insert to reader");
    }

    [Fact]
    public void Render_DropTable_OmitsChildColumnLines()
    {
        // Arrange
        var plan = new MigrationPlan([
            new DropTable("app", "audit_log"),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        // The DropTable line itself is suppressed; the group header is the only signal.
        output.ShouldContain("- table app.audit_log");
        output.ShouldNotContain("- table audit_log\n");
    }

    [Fact]
    public void Render_SchemaModifications_AreGroupedUnderSchemaHeader()
    {
        // Arrange
        var plan = new MigrationPlan([
            new RenameSchema("app_old", "app"),
            new SetSchemaComment("app", null, "new comment"),
            new GrantSchemaUsage("app", "reader"),
            new RevokeSchemaUsage("app", "writer"),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("~ schema app");
        output.ShouldContain("rename: app_old → app");
        output.ShouldContain("comment: <none> → \"new comment\"");
        output.ShouldContain("+ grant usage to reader");
        output.ShouldContain("- revoke usage from writer");
    }

    [Fact]
    public void Render_ColumnModifications_DescribeOldAndNewValues()
    {
        // Arrange
        var plan = new MigrationPlan([
            new AlterColumnType("app", "users", "id", SqlType.Int, SqlType.BigInt),
            new AlterColumnNullability("app", "users", "email", false, true),
            new SetColumnDefault("app", "users", "status", null, "'active'"),
            new SetColumnComment("app", "users", "email", "old", "new"),
            new RenameColumn("app", "users", "uname", "username"),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("~ table app.users");
        output.ShouldContain("id type: Int → BigInt");
        output.ShouldContain("email nullable: false → true");
        output.ShouldContain("status default: <none> → 'active'");
        output.ShouldContain("email comment: \"old\" → \"new\"");
        output.ShouldContain("rename column: uname → username");
    }

    [Fact]
    public void Render_IndexAndKeyChangesOnExistingTable_AppearAsModifyGroup()
    {
        // Arrange
        var plan = new MigrationPlan([
            new DropPrimaryKey("app", "orders", "orders_pkey_old"),
            new AddPrimaryKey("app", "orders", new PrimaryKey("orders_pkey", ["id"])),
            new DropForeignKey("app", "orders", "orders_user_fk_old"),
            new AddForeignKey("app", "orders", ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"])),
            new DropIndex("app", "orders", "orders_old_ix"),
            new CreateIndex("app", "orders", TableIndex.Create("orders_user_ix", ["user_id"])),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("~ table app.orders");
        output.ShouldContain("- primary key orders_pkey_old");
        output.ShouldContain("+ primary key orders_pkey");
        output.ShouldContain("- foreign key orders_user_fk_old");
        output.ShouldContain("+ foreign key orders_user_fk");
        output.ShouldContain("- index orders_old_ix");
        output.ShouldContain("+ index orders_user_ix");
    }

    [Fact]
    public void Render_DeploymentScripts_AreListedInSeparateSections()
    {
        // Arrange
        var plan = new MigrationPlan([
            new CreateSchema("app"),
            new RunScript(new Script("before.sql", "SELECT 1", ScriptType.PreDeployment)),
            new RunScript(new Script("after.sql", "SELECT 2", ScriptType.PostDeployment)),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        output.ShouldContain("Pre-deployment scripts:");
        output.ShouldContain("• before.sql");
        output.ShouldContain("Post-deployment scripts:");
        output.ShouldContain("• after.sql");
    }

    [Fact]
    public void Render_SortsSchemaGroupsBeforeTableGroups()
    {
        // Arrange
        var plan = new MigrationPlan([
            new AddColumn("app", "users", Column.Create("email", SqlType.Text)),
            new CreateSchema("app"),
        ], DatabaseSchema.Create([]));

        // Act
        var output = _sut.Render(plan);

        // Assert
        var schemaIndex = output.IndexOf("schema app", StringComparison.Ordinal);
        var tableIndex = output.IndexOf("table app.users", StringComparison.Ordinal);
        schemaIndex.ShouldBeGreaterThanOrEqualTo(0);
        tableIndex.ShouldBeGreaterThanOrEqualTo(0);
        schemaIndex.ShouldBeLessThan(tableIndex);
    }
}
