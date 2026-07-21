using NSchema.Configuration.Plugins;
using NSchema.Plugins.Model;
using NSchema.Plugins.Model.Config;

namespace NSchema.Tests.Configuration.Plugins;

/// <summary>
/// Pins how a <c>DATABASE</c>/<c>STATE</c> statement's label resolves against the declared <c>PLUGIN</c>
/// dependencies: the declaration supplies the package and pinned version, the statement supplies the settings.
/// </summary>
public sealed class PluginReferenceTests
{
    [Fact]
    public void Resolve_DeclaredLabel_CarriesPackageVersionAndConfig()
    {
        // Arrange
        var plugins = new[] { Declaration("postgres", "NSchema.Postgres", "5.0.0") };
        var config = Config("postgres", ("connection_string", ConfigValue.OfString("Host=localhost")));

        // Act
        var reference = PluginReference.Resolve(config, plugins);

        // Assert
        reference.PackageId.ShouldBe("NSchema.Postgres");
        reference.Version.ShouldBe("5.0.0");
        reference.Label.ShouldBe("postgres");
        reference.Config.Attribute("connection_string")!.AsString().ShouldBe("Host=localhost");
    }

    [Fact]
    public void Resolve_ExactPin_UsesIntervalNotationForRestore()
    {
        // Arrange — an exact pin restores as "[x.y.z]" so NuGet treats it as exact, not a minimum.
        var plugins = new[] { Declaration("postgres", "NSchema.Postgres", "5.0.0-alpha.2") };

        // Act
        var reference = PluginReference.Resolve(Config("postgres"), plugins);

        // Assert
        reference.Version.ShouldBe("5.0.0-alpha.2");
        reference.RestoreVersion.ShouldBe("[5.0.0-alpha.2]");
    }

    [Fact]
    public void Resolve_Range_KeepsCanonicalIntervalForBoth()
    {
        // Arrange
        var plugins = new[] { Declaration("postgres", "NSchema.Postgres", "[5.0,6.0)") };

        // Act
        var reference = PluginReference.Resolve(Config("postgres"), plugins);

        // Assert
        reference.Version.ShouldBe("[5.0.0,6.0.0)");
        reference.RestoreVersion.ShouldBe("[5.0.0,6.0.0)");
    }

    [Fact]
    public void Resolve_LabelMatch_IsCaseInsensitive()
    {
        // Arrange
        var plugins = new[] { Declaration("Postgres", "NSchema.Postgres", "5.0.0") };

        // Act
        var reference = PluginReference.Resolve(Config("postgres"), plugins);

        // Assert
        reference.PackageId.ShouldBe("NSchema.Postgres");
    }

    [Fact]
    public void Resolve_UndeclaredLabel_ThrowsAndSuggestsPluginStatement()
    {
        // Act / Assert
        Should.Throw<InvalidOperationException>(() => PluginReference.Resolve(Config("oracle"), []))
            .Message.ShouldContain("PLUGIN oracle");
    }

    private static PluginDeclaration Declaration(string label, string packageId, string version) =>
        new(new PluginLabel(label), new PackageId(packageId), VersionRange.Parse(version));

    private static PluginConfig Config(string label, params (string Key, ConfigValue Value)[] attributes)
        => new(new PluginLabel(label), attributes.ToDictionary(a => new AttributeKey(a.Key), a => a.Value));
}
