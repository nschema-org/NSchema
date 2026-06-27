using NSchema.Configuration;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Configuration.Plugins;

/// <summary>
/// Pins how a <c>PROVIDER</c>/<c>BACKEND</c> block resolves to a plugin package: the built-in label map, the
/// <c>source</c> override (open ecosystem — any package id), the required pinned <c>version</c>, and the
/// stripping of those CLI-level attributes before the block reaches the plugin.
/// </summary>
public sealed class PluginReferenceTests
{
    private static readonly Dictionary<string, string> BuiltIn =
        new(StringComparer.OrdinalIgnoreCase) { ["postgres"] = "NSchema.Postgres" };

    [Fact]
    public void FromBlock_KnownLabel_MapsToBuiltInPackage()
    {
        // Arrange
        var block = Block("postgres",
            ("version", ConfigValue.OfString("4.0.0")),
            ("connection_string", ConfigValue.OfString("Host=localhost")));

        // Act
        var reference = PluginReference.FromBlock(block, BuiltIn);

        // Assert
        reference.PackageId.ShouldBe("NSchema.Postgres");
        reference.Version.ShouldBe("4.0.0");
        reference.Label.ShouldBe("postgres");
    }

    [Fact]
    public void FromBlock_Source_OverridesBuiltInWithAnyPackage()
    {
        // Arrange — a third-party provider names its own package.
        var block = Block("oracle",
            ("source", ConfigValue.OfString("Acme.NSchema.Oracle")),
            ("version", ConfigValue.OfString("1.2.3")));

        // Act
        var reference = PluginReference.FromBlock(block, BuiltIn);

        // Assert
        reference.PackageId.ShouldBe("Acme.NSchema.Oracle");
        reference.Label.ShouldBe("oracle");
    }

    [Fact]
    public void FromBlock_StripsVersionAndSource_SoThePluginNeverSeesThem()
    {
        // Arrange
        var block = Block("postgres",
            ("version", ConfigValue.OfString("4.0.0")),
            ("source", ConfigValue.OfString("NSchema.Postgres")),
            ("connection_string", ConfigValue.OfString("Host=localhost")));

        // Act
        var reference = PluginReference.FromBlock(block, BuiltIn);

        // Assert
        reference.Block.Attribute("version").ShouldBeNull();
        reference.Block.Attribute("source").ShouldBeNull();
        reference.Block.Attribute("connection_string").ShouldNotBeNull();
    }

    [Fact]
    public void FromBlock_LabelIsLowercased()
    {
        // Arrange
        var block = Block("Postgres", ("version", ConfigValue.OfString("4.0.0")));

        // Act
        var reference = PluginReference.FromBlock(block, BuiltIn);

        // Assert
        reference.Label.ShouldBe("postgres");
        reference.PackageId.ShouldBe("NSchema.Postgres");
    }

    [Fact]
    public void FromBlock_UnknownLabelWithoutSource_ThrowsAndSuggestsSource()
    {
        // Arrange
        var block = Block("oracle", ("version", ConfigValue.OfString("1.0.0")));

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => PluginReference.FromBlock(block, BuiltIn))
            .Message.ShouldContain("source");
    }

    [Fact]
    public void FromBlock_MissingVersion_Throws()
    {
        // Arrange
        var block = Block("postgres", ("connection_string", ConfigValue.OfString("Host=localhost")));

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => PluginReference.FromBlock(block, BuiltIn))
            .Message.ShouldContain("version");
    }

    [Fact]
    public void FromBlock_InvalidPackageId_Throws()
    {
        // Arrange
        var block = Block("oracle",
            ("source", ConfigValue.OfString("not a valid id")),
            ("version", ConfigValue.OfString("1.0.0")));

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => PluginReference.FromBlock(block, BuiltIn))
            .Message.ShouldContain("valid NuGet package id");
    }

    private static ConfigBlock Block(string label, params (string Key, ConfigValue Value)[] attributes)
        => new("provider", label, attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase));
}
