using NSchema.Plugins;

namespace NSchema.Tests.Plugins;

/// <summary>
/// The engine handshake: the NSchema.Core version a plugin was compiled against determines compatibility —
/// the majors must match, and the engine must be at least as new as the plugin's build target.
/// </summary>
public sealed class PluginHandshakeTests
{
    private static readonly Version Host = new(5, 2, 0, 0);

    [Fact]
    public void Validate_SameVersion_Passes()
        => PluginHandshake.Validate("pg", new Version(5, 2, 0, 0), Host).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Validate_PluginOlderThanEngine_Passes()
        => PluginHandshake.Validate("pg", new Version(5, 0, 0, 0), Host).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Validate_PatchDifference_IsIgnored()
        => PluginHandshake.Validate("pg", new Version(5, 2, 7, 0), Host).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Validate_PluginNewerThanEngine_Fails()
    {
        // Arrange
        var referenced = new Version(5, 3, 0, 0);

        // Act
        var result = PluginHandshake.Validate("pg", referenced, Host);

        // Assert
        result.Errors.ShouldHaveSingleItem().ShouldBe(HandshakeDiagnostics.EngineOlderThanPlugin("pg", referenced, Host));
    }

    [Fact]
    public void Validate_MajorMismatch_Fails()
    {
        // Arrange
        var referenced = new Version(4, 9, 0, 0);

        // Act
        var result = PluginHandshake.Validate("pg", referenced, Host);

        // Assert
        result.Errors.ShouldHaveSingleItem().ShouldBe(HandshakeDiagnostics.MajorMismatch("pg", referenced, Host));
    }

    [Fact]
    public void Validate_AssemblyReferencingHostCore_Passes()
        => PluginHandshake.Validate(typeof(PluginHandshakeTests).Assembly).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Validate_AssemblyWithoutCoreReference_IsNotAPlugin()
    {
        // Arrange
        var assembly = typeof(object).Assembly;

        // Act
        var result = PluginHandshake.Validate(assembly);

        // Assert
        result.Errors.ShouldHaveSingleItem().ShouldBe(HandshakeDiagnostics.DoesNotReferenceCore(assembly.GetName().Name!));
    }
}
