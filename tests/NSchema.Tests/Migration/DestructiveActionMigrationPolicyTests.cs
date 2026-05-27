using Microsoft.Extensions.Options;
using NSchema.Hosting;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Schema;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Migration;

public class DestructiveActionMigrationPolicyTests
{
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());
    private readonly IMigrationReporter _reporter = Substitute.For<IMigrationReporter>();

    private readonly DestructiveActionMigrationPolicy _sut;

    public DestructiveActionMigrationPolicyTests()
    {
        _sut = new DestructiveActionMigrationPolicy(_options, _reporter);
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsErrorForDestructiveAction()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Error;

        // Act
        var errors = _sut.Validate(TestData.DestructivePlan).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].PolicyName.ShouldBe(nameof(DestructiveActionMigrationPolicy));
        errors[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_WhenPolicyIsAllow_ReturnsNoErrors()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Allow;

        // Act
        var errors = _sut.Validate(TestData.DestructivePlan).ToList();

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsWarn_ReturnsNoErrors()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Warn;

        // Act
        var errors = _sut.Validate(TestData.DestructivePlan).ToList();

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NonDestructiveAction_ReturnsNoErrorsRegardlessOfPolicy()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Error;

        // Act
        var errors = _sut.Validate(TestData.NonDestructivePlan).ToList();

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsOneErrorPerDestructiveActionType()
    {
        // Arrange
        var plan = new MigrationPlan([TestData.DestructiveAction, TestData.DestructiveAction], DatabaseSchema.Create([]));
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Error;

        // Act
        var errors = _sut.Validate(plan).ToList();

        // Assert
        errors.Count.ShouldBe(1);
    }
}
