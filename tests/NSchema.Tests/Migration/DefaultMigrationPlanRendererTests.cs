using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class DefaultMigrationPlanRendererTests
{
    // One sample per concrete MigrationAction subtype. Passing each through Render() exercises
    // the renderer's switches; any missing case hits the `_ => throw NotSupportedException`
    // default and fails the coverage test below.
    private static readonly IReadOnlyDictionary<Type, MigrationAction> SamplesByType =
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
        }.ToDictionary(a => a.GetType());

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
        // Catches "added a new MigrationAction subtype but forgot to add a sample below" before
        // the per-type render test runs.
        var allActionTypes = typeof(MigrationAction).Assembly
            .GetTypes()
            .Where(t => t is { IsSealed: true, IsAbstract: false } && t.IsAssignableTo(typeof(MigrationAction)))
            .ToList();

        allActionTypes.ShouldNotBeEmpty();
        foreach (var type in allActionTypes)
        {
            SamplesByType.ShouldContainKey(type, $"Missing renderer test sample for {type.Name}");
        }
    }

    [Theory]
    [MemberData(nameof(AllConcreteActionTypes))]
    public void Render_HandlesEveryConcreteActionType(Type actionType)
    {
        var sample = SamplesByType[actionType];

        // Should not throw — the switches in the renderer have `_ => throw` defaults, so a
        // missing case for any new action type will fail here.
        var output = Should.NotThrow(() => new DefaultMigrationPlanRenderer().Render(new MigrationPlan([sample], DatabaseSchema.Create([]))));

        output.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Render_EmptyPlan_ReturnsNoChangesMessage()
    {
        var renderer = new DefaultMigrationPlanRenderer();

        var output = renderer.Render(new MigrationPlan([], DatabaseSchema.Create([])));

        output.ShouldBe("No changes detected.");
    }

    [Fact]
    public void Render_GroupsActionsByTable_AndIncludesSummaryHeader()
    {
        var renderer = new DefaultMigrationPlanRenderer();
        var plan = new MigrationPlan([
            new CreateSchema("app"),
            new AddColumn("app", "orders", Column.Create("shipped_at", SqlType.DateTimeOffset)),
            new DropTable("app", "audit_log"),
        ], DatabaseSchema.Create([]));

        var output = renderer.Render(plan);

        output.ShouldContain("Plan: 1 to add, 1 to change, 1 to destroy.");
        output.ShouldContain("schema app");
        output.ShouldContain("table app.orders");
        output.ShouldContain("shipped_at");
        output.ShouldContain("table app.audit_log");
    }
}
