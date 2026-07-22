using NSchema.Commands.Script.Untaint;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Script.Untaint;

public sealed class ScriptUntaintConfigurationValidatorTests
{
    private readonly ScriptUntaintConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange — no provider needed: the recorded hash comes from the script's declaration.
        var config = new ScriptUntaintConfiguration
        {
            State = new StateConfiguration { File = new FileStateConfiguration { Path = "./state.json" } },
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
        var config = new ScriptUntaintConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
