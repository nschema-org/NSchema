using NSchema.Configuration;
using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Configuration;

/// <summary>
/// Which origin a <c>PLUGIN</c> statement declares. A plugin comes from a package to resolve or from an assembly
/// already built on disk, and binding cannot decide which — what is required depends on the answer — so the
/// combination is judged during assembly.
/// </summary>
public sealed class PluginOriginTests
{
    private static NsqlDocument Doc(string source)
    {
        var result = NsqlReader.Read(source);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static Result<ConfigurationDefinition, NsqlDiagnostic> Assemble(string plugin) =>
        ConfigurationAssembler.Assemble([Doc(plugin)]);

    [Fact]
    public void SourceAndVersion_IsAPackageOrigin()
    {
        // Act
        var result = Assemble("PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var origin = result.Value.Plugins.ShouldHaveSingleItem().Origin.ShouldBeOfType<PackageOrigin>();
        origin.Package.Source.ShouldBe(new PackageId("NSchema.Postgres"));
        origin.Package.Version.ShouldBe(VersionRange.Parse("5.0.1"));
    }

    [Fact]
    public void Path_IsAPathOrigin()
    {
        // Act
        var result = Assemble("PLUGIN pg ( path = './artifacts/NSchema.Postgres.dll' );");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().Origin
            .ShouldBeOfType<PathOrigin>().Path.ShouldBe("./artifacts/NSchema.Postgres.dll");
    }

    [Fact]
    public void Path_IsNotAPackage()
    {
        // The convenience that the package-only call sites lean on — resolving a range, locking a version,
        // reporting what is outdated — has to be absent for a path, or each of them would silently do the wrong
        // thing rather than skip it.

        // Act
        var result = Assemble("PLUGIN pg ( path = './NSchema.Postgres.dll' );");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().Package.ShouldBeNull();
    }

    [Theory]
    [InlineData("PLUGIN pg ( path = './x.dll', source = 'NSchema.Postgres', version = '5.0.1' );")]
    [InlineData("PLUGIN pg ( path = './x.dll', source = 'NSchema.Postgres' );")]
    [InlineData("PLUGIN pg ( path = './x.dll', version = '5.0.1' );")]
    public void PathMixedWithPackageAttributes_IsRejected(string plugin)
    {
        // Rejected rather than ignored: a package attribute beside a path reads as if it pins something, and it
        // pins nothing. Which attribute it was does not change the answer, so one diagnostic covers all three.

        // Act
        var result = Assemble(plugin);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Code == "conflicting-plugin-origin");
    }

    [Theory]
    [InlineData("PLUGIN pg ( );")]
    [InlineData("PLUGIN pg ( source = 'NSchema.Postgres' );")]
    [InlineData("PLUGIN pg ( version = '5.0.1' );")]
    public void NoCompleteOrigin_IsRejected(string plugin)
    {
        // A source without a version resolves nothing, so it is as incomplete as declaring neither.

        // Act
        var result = Assemble(plugin);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Code == "missing-plugin-origin");
    }

    [Fact]
    public void TwoPathsAreNotADuplicate()
    {
        // Declaring one package twice is a mistake — it is declared once and referenced by label. Two paths are
        // not the same thing: a path names bits rather than an identity, and nothing downstream keys on it.

        // Act
        var result = ConfigurationAssembler.Assemble(
        [
            Doc("PLUGIN a ( path = './one/NSchema.Postgres.dll' );"),
            Doc("PLUGIN b ( path = './two/NSchema.Postgres.dll' );"),
        ]);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.Count.ShouldBe(2);
    }

    [Fact]
    public void TwoDeclarationsOfOnePackage_IsStillADuplicate()
    {
        // Act
        var result = ConfigurationAssembler.Assemble(
        [
            Doc("PLUGIN a ( source = 'NSchema.Postgres', version = '5.0.1' );"),
            Doc("PLUGIN b ( source = 'NSchema.Postgres', version = '5.0.1' );"),
        ]);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Code == "duplicate-plugin-source");
    }
}
