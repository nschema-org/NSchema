using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class DestructiveActionMigrationPolicyTests
{
    private static DestructiveActionMigrationPolicy Create(DestructiveActionPolicy policy) => new(
        Substitute.For<IMigrationReporter>(),
        Options.Create(new MigrationOptions { DestructiveActionPolicy = policy })
    );

    private static MigrationPlan PlanWith(params MigrationAction[] actions) => new(actions, DatabaseSchema.Create([]));

    private static readonly MigrationAction DestructiveAction = new DropTable("public", "users");
    private static readonly MigrationAction NonDestructiveAction = new CreateTable("public",
        Table.Create("users", columns: [Column.Create("id", SqlType.BigInt, isNullable: false)]));

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsErrorForDestructiveAction()
    {
        var enforcer = Create(DestructiveActionPolicy.Error);

        var errors = enforcer.Validate(PlanWith(DestructiveAction)).ToList();

        errors.ShouldHaveSingleItem();
        errors[0].PolicyName.ShouldBe(nameof(DestructiveActionMigrationPolicy));
        errors[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_WhenPolicyIsAllow_ReturnsNoErrors()
    {
        var enforcer = Create(DestructiveActionPolicy.Allow);

        var errors = enforcer.Validate(PlanWith(DestructiveAction)).ToList();

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsWarn_ReturnsNoErrors()
    {
        var enforcer = Create(DestructiveActionPolicy.Warn);

        var errors = enforcer.Validate(PlanWith(DestructiveAction)).ToList();

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NonDestructiveAction_ReturnsNoErrorsRegardlessOfPolicy()
    {
        var enforcer = Create(DestructiveActionPolicy.Error);

        var errors = enforcer.Validate(PlanWith(NonDestructiveAction)).ToList();

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsOneErrorPerDestructiveActionType()
    {
        var enforcer = Create(DestructiveActionPolicy.Error);

        var errors = enforcer.Validate(PlanWith(DestructiveAction, DestructiveAction)).ToList();

        errors.Count.ShouldBe(1);
    }
}
