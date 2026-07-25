using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// The <see cref="LockFile"/> domain type: its empty value and package lookup. File I/O lives in
/// <see cref="LockFileManager"/>.
/// </summary>
public sealed class LockFileTests
{
    [Fact]
    public void Empty_HasNoPlugins()
        => LockFile.Empty.Plugins.ShouldBeEmpty();

    [Fact]
    public void Find_ReturnsTheLockedEntry()
    {
        var lockFile = new LockFile([new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse("5.0.0-alpha.2") }]);

        lockFile.Find(new PackageId("NSchema.Postgres"))!.Version.ShouldBe(SemanticVersion.Parse("5.0.0-alpha.2"));
    }

    [Fact]
    public void Find_ReturnsNull_WhenUnlocked()
        => new LockFile([]).Find(new PackageId("NSchema.Aws")).ShouldBeNull();

    [Fact]
    public void Resolve_ExactPin_ResolvesToItself()
        // An exact pin is its own resolution — it needs no lock entry.
        => new LockFile([]).Resolve(new PluginDeclaration("pg", new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("5.0.1") }))
            .Value.ShouldBe(SemanticVersion.Parse("5.0.1"));

    [Fact]
    public void Resolve_Range_ResolvesToTheLockedPin()
    {
        var lockFile = new LockFile([new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse("5.3.1") }]);

        lockFile.Resolve(new PluginDeclaration("pg", new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("[5.0,6.0)") }))
            .Value.ShouldBe(SemanticVersion.Parse("5.3.1"));
    }

    [Fact]
    public void Resolve_UnlockedRange_IsAnError()
        => new LockFile([]).Resolve(new PluginDeclaration("pg", new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("[5.0,6.0)") }))
            .IsFailure.ShouldBeTrue();
}
