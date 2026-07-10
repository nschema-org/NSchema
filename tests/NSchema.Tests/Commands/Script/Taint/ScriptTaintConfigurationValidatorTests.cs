using NSchema.Commands.Script.Taint;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Script.Taint;

public sealed class ScriptTaintConfigurationValidatorTests
{
    private readonly ScriptTaintConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange
        var config = new ScriptTaintConfiguration
        {
            State = new StateConfig { File = new FileStateConfig { Path = "./state.json" } },
        };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Invalid_WhenStateMissing()
    {
        // Arrange — the ledger lives in the state, so a store is mandatory.
        var config = new ScriptTaintConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
