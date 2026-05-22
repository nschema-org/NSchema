using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Migration.Actions;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class DestructiveActionMigrationPolicyTests
{
    private static DestructiveActionMigrationPolicy Create(DestructiveActionPolicy policy) => new(
        NullLogger<DestructiveActionMigrationPolicy>.Instance,
        Options.Create(new MigrationOptions { DestructiveActionPolicy = policy })
    );

    private static MigrationPlan PlanWith(params MigrationAction[] actions) => new(actions);

    private static readonly MigrationAction DestructiveAction = new DropTable("public", "users");
    private static readonly MigrationAction NonDestructiveAction = new CreateTable("public",
        new Table("users", Columns: [new Column("id", SqlType.BigInt, IsNullable: false)]));

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
    public void Validate_WhenPolicyIsError_ReturnsOneErrorPerDestructiveAction()
    {
        var enforcer = Create(DestructiveActionPolicy.Error);

        var errors = enforcer.Validate(PlanWith(DestructiveAction, DestructiveAction)).ToList();

        errors.Count.ShouldBe(2);
    }
}
