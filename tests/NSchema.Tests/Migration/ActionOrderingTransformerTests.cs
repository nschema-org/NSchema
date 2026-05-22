using NSchema.Migration;
using NSchema.Migration.Actions;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class ActionOrderingTransformerTests
{
    private readonly ActionOrderingTransformer _sut = new();

    private static MigrationPlan PlanWith(params MigrationAction[] actions) => new(actions);

    [Fact]
    public void Transform_OrdersForeignKeyDropBeforeColumnDrop()
    {
        // Arrange
        var plan = PlanWith(
            new DropColumn("app", "users", "org_id"),
            new DropForeignKey("app", "users", "fk_users_org"));

        // Act
        var result = _sut.Transform(plan).Actions.ToList();

        // Assert
        result.FindIndex(i => i is DropForeignKey).ShouldBeLessThan(
            result.FindIndex(i => i is DropColumn));
    }

    [Fact]
    public void Transform_OrdersForeignKeyAddAfterTableCreate()
    {
        // Arrange
        var fk = new ForeignKey("fk_orders_user", ["user_id"], "app", "users", ["id"]);
        var plan = PlanWith(
            new AddForeignKey("app", "orders", fk),
            new CreateTable("app", new Table("users", Columns: [new Column("id", SqlType.Int, IsNullable: false)])),
            new CreateTable("app", new Table("orders", Columns: [new Column("id", SqlType.Int, IsNullable: false)])));

        // Act
        var result = _sut.Transform(plan).Actions.ToList();

        // Assert
        result.FindLastIndex(i => i is CreateTable).ShouldBeLessThan(
            result.FindIndex(i => i is AddForeignKey));
    }

    [Fact]
    public void Transform_OrdersPreDeploymentScriptFirst()
    {
        // Arrange
        var plan = PlanWith(
            new CreateTable("app", new Table("items", Columns: [new Column("id", SqlType.Int, IsNullable: false)])),
            new RunPreDeploymentScript(new Script("pre", "SELECT 1")));

        // Act
        var result = _sut.Transform(plan).Actions.ToList();

        // Assert
        result[0].ShouldBeOfType<RunPreDeploymentScript>();
    }

    [Fact]
    public void Transform_OrdersPostDeploymentScriptLast()
    {
        // Arrange
        var plan = PlanWith(
            new RunPostDeploymentScript(new Script("post", "SELECT 1")),
            new CreateTable("app", new Table("items", Columns: [new Column("id", SqlType.Int, IsNullable: false)])));

        // Act
        var result = _sut.Transform(plan).Actions.ToList();

        // Assert
        result[^1].ShouldBeOfType<RunPostDeploymentScript>();
    }

    [Fact]
    public void Transform_OrdersTableDropAfterColumnDrop()
    {
        // Arrange
        var plan = PlanWith(
            new DropTable("app", "users"),
            new DropColumn("app", "orders", "user_id"));

        // Act
        var result = _sut.Transform(plan).Actions.ToList();

        // Assert
        result.FindIndex(i => i is DropColumn).ShouldBeLessThan(
            result.FindIndex(i => i is DropTable));
    }

    [Fact]
    public void Priorities_RegistersEveryConcreteActionType()
    {
        // Arrange
        var allActionTypes = typeof(MigrationAction).Assembly
            .GetTypes()
            .Where(t => t is { IsSealed: true, IsAbstract: false } && t.IsAssignableTo(typeof(MigrationAction)))
            .ToList();

        // Assert
        allActionTypes.ShouldNotBeEmpty();
        foreach (var type in allActionTypes)
            ActionOrderingTransformer.Priorities.ShouldContainKey(type, $"{type.Name} is missing from ActionOrderingTransformer");
    }
}
