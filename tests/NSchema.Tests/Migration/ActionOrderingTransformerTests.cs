using NSchema.Domain.Migration;
using NSchema.Domain.Migration.Actions;
using NSchema.Domain.Schema;
using NSchema.Migration;

namespace NSchema.Tests.Migration;

public class ActionOrderingTransformerTests
{
    private readonly ActionOrderingTransformer _transformer = new();

    private static MigrationPlan PlanWith(params SchemaAction[] actions) => new(actions);

    [Fact]
    public void Transform_OrdersForeignKeyDropBeforeColumnDrop()
    {
        var plan = PlanWith(
            new DropColumn("app", "users", "org_id"),
            new DropForeignKey("app", "users", "fk_users_org"));

        var result = _transformer.Transform(plan).Actions.ToList();

        result.FindIndex(i => i is DropForeignKey).ShouldBeLessThan(
            result.FindIndex(i => i is DropColumn));
    }

    [Fact]
    public void Transform_OrdersForeignKeyAddAfterTableCreate()
    {
        var fk = new ForeignKey("fk_orders_user", ["user_id"], "app", "users", ["id"]);
        var plan = PlanWith(
            new AddForeignKey("app", "orders", fk),
            new CreateTable("app", new Table("users", [new Column("id", SqlType.Int, IsNullable: false)])),
            new CreateTable("app", new Table("orders", [new Column("id", SqlType.Int, IsNullable: false)])));

        var result = _transformer.Transform(plan).Actions.ToList();

        result.FindLastIndex(i => i is CreateTable).ShouldBeLessThan(
            result.FindIndex(i => i is AddForeignKey));
    }

    [Fact]
    public void Transform_OrdersPreDeploymentScriptFirst()
    {
        var plan = PlanWith(
            new CreateTable("app", new Table("items", [new Column("id", SqlType.Int, IsNullable: false)])),
            new RunPreDeploymentScript(new Script("pre", "SELECT 1")));

        var result = _transformer.Transform(plan).Actions.ToList();

        result[0].ShouldBeOfType<RunPreDeploymentScript>();
    }

    [Fact]
    public void Transform_OrdersPostDeploymentScriptLast()
    {
        var plan = PlanWith(
            new RunPostDeploymentScript(new Script("post", "SELECT 1")),
            new CreateTable("app", new Table("items", [new Column("id", SqlType.Int, IsNullable: false)])));

        var result = _transformer.Transform(plan).Actions.ToList();

        result[^1].ShouldBeOfType<RunPostDeploymentScript>();
    }

    [Fact]
    public void Transform_OrdersTableDropAfterColumnDrop()
    {
        var plan = PlanWith(
            new DropTable("app", "users"),
            new DropColumn("app", "orders", "user_id"));

        var result = _transformer.Transform(plan).Actions.ToList();

        result.FindIndex(i => i is DropColumn).ShouldBeLessThan(
            result.FindIndex(i => i is DropTable));
    }
}
