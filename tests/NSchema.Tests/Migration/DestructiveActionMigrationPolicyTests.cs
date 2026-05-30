using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Migration.Plan;
using NSchema.Policies;
using NSchema.Schema;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Migration;

public class DestructiveActionMigrationPolicyTests
{
    private readonly IOptions<MigrationOptions> _options = Options.Create(new MigrationOptions());

    private readonly DestructiveActionMigrationPolicy _sut;

    public DestructiveActionMigrationPolicyTests()
    {
        _sut = new DestructiveActionMigrationPolicy(_options);
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
        errors[0].Severity.ShouldBe(PolicySeverity.Error);
        errors[0].PolicyName.ShouldBe(nameof(DestructiveActionMigrationPolicy));
        errors[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_WhenPolicyIsAllow_ReturnsInfoDiagnostic()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Allow;

        // Act
        var results = _sut.Validate(TestData.DestructivePlan).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(PolicySeverity.Info);
    }

    [Fact]
    public void Validate_WhenPolicyIsWarn_ReturnsWarningDiagnostic()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Warn;

        // Act
        var results = _sut.Validate(TestData.DestructivePlan).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(PolicySeverity.Warning);
        results[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_NonDestructiveAction_ReturnsNothingRegardlessOfPolicy()
    {
        // Arrange
        _options.Value.DestructiveActionPolicy = DestructiveActionPolicy.Error;

        // Act
        var results = _sut.Validate(TestData.NonDestructivePlan).ToList();

        // Assert
        results.ShouldBeEmpty();
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
