using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSchema.Domain.Migration;
using NSchema.Domain.Migration.Actions;
using NSchema.Domain.Schema;
using NSchema.Migration;

namespace NSchema.Tests.Migration;

public class DestructiveActionPolicyEnforcerTests
{
    private static DestructiveActionPolicyEnforcer Create(DestructiveActionPolicy policy) => new(
        NullLogger<DestructiveActionPolicyEnforcer>.Instance,
        Options.Create(new MigrationOptions { DestructiveActionPolicy = policy })
    );

    private static MigrationPlan PlanWith(params SchemaAction[] actions) => new(actions);

    private static readonly SchemaAction DestructiveAction = new DropTable("public", "users");
    private static readonly SchemaAction NonDestructiveAction = new CreateTable("public",
        new Table("users", [new Column("id", SqlType.BigInt, IsNullable: false)]));

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsErrorForDestructiveAction()
    {
        var enforcer = Create(DestructiveActionPolicy.Error);

        var errors = enforcer.Validate(PlanWith(DestructiveAction)).ToList();

        errors.ShouldHaveSingleItem();
        errors[0].PolicyName.ShouldBe(nameof(DestructiveActionPolicyEnforcer));
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
