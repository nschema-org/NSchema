using NSchema.Commands.Script.List;
using NSchema.Configuration.State;

namespace NSchema.Tests.Commands.Script.List;

public sealed class ScriptListConfigurationValidatorTests
{
    private readonly ScriptListConfigurationValidator _sut = new();

    [Fact]
    public void Valid_WithStateStore()
    {
        // Arrange
        var config = new ScriptListConfiguration
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
        var config = new ScriptListConfiguration { State = null };

        // Act
        var result = _sut.Validate(config);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.ErrorMessage.Contains("state store is required"));
    }
}
