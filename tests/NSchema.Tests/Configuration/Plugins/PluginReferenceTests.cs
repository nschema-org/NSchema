using NSchema.Configuration.Model;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Configuration.Plugins;

/// <summary>
/// Pins how a <c>DATABASE</c>/<c>STATE</c> statement's label resolves against the declared <c>PLUGIN</c>
/// dependencies: the declaration supplies the package and pinned version, the statement supplies the settings.
/// </summary>
public sealed class PluginReferenceTests
{
    [Fact]
    public void Resolve_DeclaredLabel_CarriesPackageVersionAndConfiguration()
    {
        // Arrange
        var plugins = new[] { Declaration("postgres", "NSchema.Postgres", "5.0.0") };
        var config = Settings("postgres", ("connection_string", "Host=localhost"));

        // Act
        var reference = PluginReference.Resolve(config, plugins, Resolver(new SemanticVersion(5, 0, 0, 0, null)));

        // Assert
        reference.PackageId.ShouldBe("NSchema.Postgres");
        reference.Version.ToString().ShouldBe("5.0.0");
        reference.Label.ShouldBe("postgres");
        reference.Settings.Attribute("connection_string")!.ShouldBe("Host=localhost");
    }

    [Fact]
    public void Resolve_ExactPin_TakesTheResolvedVersion()
    {
        // Arrange — an exact pin no longer self-resolves; like every plugin its version comes from the lockfile, so
        // the resolved version is authoritative even where it differs from the declared text.
        var plugins = new[] { Declaration("postgres", "NSchema.Postgres", "5.0.0") };

        // Act
        var reference = PluginReference.Resolve(Settings("postgres"), plugins, Resolver(new SemanticVersion(5, 0, 1, 0, null)));

        // Assert
        reference.Version.ToString().ShouldBe("5.0.1");
    }

    [Fact]
    public void Resolve_Range_PinsToResolvedVersion()
    {
        // Arrange — a range is pinned to the concrete version the resolver returns; that pin is the display, cache
        // key, and (as "[x.y.z]") the exact restore.
        var plugins = new[] { Declaration("postgres", "NSchema.Postgres", "[5.0,6.0)") };

        // Act
        var reference = PluginReference.Resolve(Settings("postgres"), plugins, Resolver(new SemanticVersion(5, 3, 1, 0, null)));

        // Assert
        reference.Version.ToString().ShouldBe("5.3.1");
    }

    [Fact]
    public void Resolve_LabelMatch_IsCaseInsensitive()
    {
        // Arrange
        var plugins = new[] { Declaration("Postgres", "NSchema.Postgres", "5.0.0") };

        // Act
        var reference = PluginReference.Resolve(Settings("postgres"), plugins, Resolver(new SemanticVersion(5, 0, 0, 0, null)));

        // Assert
        reference.PackageId.ShouldBe("NSchema.Postgres");
    }

    [Fact]
    public void Resolve_UndeclaredLabel_ThrowsAndSuggestsPluginStatement()
    {
        // Act / Assert
        Should.Throw<InvalidOperationException>(() => PluginReference.Resolve(Settings("oracle"), [], Resolver()))
            .Message.ShouldContain("PLUGIN oracle");
    }

    private static PluginDeclaration Declaration(string label, string packageId, string version) =>
        new(new PluginLabel(label), new PackageReference { Source = new PackageId(packageId), Version = VersionRange.Parse(version) });

    private static PluginSettings Settings(string label, params (string Key, string? Value)[] attributes)
        => new(new PluginLabel(label), attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase));

    // Stands in for the lockfile: every plugin (exact or range) resolves through it, so a test supplies the version
    // the plugin is locked to.
    private static Func<PackageId, VersionRange, SemanticVersion> Resolver(SemanticVersion? resolved = null) =>
        (source, range) => resolved ?? throw new InvalidOperationException($"'{source}' is not locked.");
}
