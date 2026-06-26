using NSchema.Commands.Plan;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Plan;

public sealed class PlanConfigurationValidatorTests
{
    private readonly PlanConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithProviderOnly()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Provider = TestConfigs.Provider(),
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_WithStateOnly_ForOfflinePlanning()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Provider = new ProviderConfig(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenNeitherProviderNorStateConfigured()
    {
        // Arrange
        var config = new PlanConfiguration
        {
            Provider = new ProviderConfig(),
            State = new StateConfig(),
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("current schema source"));
    }

    [Fact]
    public void Valid_ForDestroy_WithProviderAndStateOnly()
    {
        // Arrange — --destroy tears down the recorded state.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Provider = TestConfigs.Provider(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForDestroy_WithProviderOnly_FallsBackToWorkingDirectorySchema()
    {
        // Arrange — with no state store, --destroy falls back to the working-directory schema as the teardown source.
        var config = new PlanConfiguration
        {
            Destroy = true,
            Provider = TestConfigs.Provider(),
            State = new StateConfig(),
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
            State = new StateConfig(),
            OutFile = "plan.nschema",
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Valid_ForDestroy_WithOutFile()
    {
        // Arrange — a teardown preview can also be saved for later replay (PlanDestroyArguments.OutFile).
        var config = new PlanConfiguration
        {
            Destroy = true,
            Provider = TestConfigs.Provider(),
            State = new StateConfig(),
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
            Provider = new ProviderConfig(),
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("database provider is required"));
    }
}
