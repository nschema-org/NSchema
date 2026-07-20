using NSchema.Commands.Plan;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Plan;

public sealed class PlanConfigurationValidatorTests
{
    private readonly PlanConfigurationValidator _sut = new();

    private static StateConfig FileState() => new() { File = new FileStateConfig { Path = "./state.json" } };

    [Fact]
    public void Valid_WithProviderAndState()
    {
        // Arrange — a plan renders SQL against the provider and diffs the recorded state, so it needs both.
        var config = new PlanConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = FileState(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenProviderMissing()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Provider = null,
            State = FileState(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange — planning always diffs the recorded state, so a store is mandatory.
        var config = new PlanConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }

    [Fact]
    public void Valid_WithEphemeral_InsteadOfAStore()
    {
        // Arrange — --ephemeral stands in for a configured store (CI against a disposable database).
        var config = new PlanConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = null,
            Ephemeral = true,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForDestroy_WithProviderAndState()
    {
        // Arrange — --destroy tears down the managed schema recorded in the state.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Provider = TestConfigs.Provider(),
            State = FileState(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForForwardPlan_WithOutFile()
    {
        // Arrange — a forward plan can be saved for later replay.
        var config = new PlanConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = FileState(),
            OutFile = "plan.nschema",
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_ForDestroy_WhenProviderMissing()
    {
        // Arrange — the teardown SQL is rendered against the provider, so one is required.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Provider = null,
            State = FileState(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }

    [Fact]
    public void Invalid_ForDestroy_WhenStateMissing()
    {
        // Arrange — the managed schema is read from the recorded state, so a store is required.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Provider = TestConfigs.Provider(),
            State = null,
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
