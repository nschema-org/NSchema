using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Plugins.Model;

/// <summary>
/// The <see cref="LockFile"/> domain type: its empty value and package lookup. File I/O lives in
/// <see cref="LockFileManager"/>.
/// </summary>
public sealed class LockFileTests
{
    private static LockedPlugin Pin(string source, string version) =>
        new() { Source = new PackageId(source), Version = SemanticVersion.Parse(version) };

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
    public void With_ReplacesTheEntryForTheSameSource()
    {
        // Arrange
        var lockFile = new LockFile([Pin("NSchema.Postgres", "5.0.0"), Pin("NSchema.Aws", "5.1.0")]);

        // Act
        var updated = lockFile.With([Pin("NSchema.Postgres", "5.2.0")]);

        // Assert — replaced in place, so the order the file was written in survives.
        updated.Plugins.Select(p => (p.Source.ToString(), p.Version.ToString()))
            .ShouldBe([("NSchema.Postgres", "5.2.0"), ("NSchema.Aws", "5.1.0")]);
    }

    [Fact]
    public void With_KeepsPinsTheResolutionDoesNotMention()
        // A resolution only covers one environment's plugins, so another environment's pin must survive it.
        => new LockFile([Pin("NSchema.Sqlite", "5.0.0")]).With([Pin("NSchema.Postgres", "5.2.0")])
            .Find(new PackageId("NSchema.Sqlite"))!.Version.ShouldBe(SemanticVersion.Parse("5.0.0"));

    [Fact]
    public void With_AppendsAnUnlockedSource()
        => LockFile.Empty.With([Pin("NSchema.Postgres", "5.2.0")])
            .Plugins.ShouldHaveSingleItem().Version.ShouldBe(SemanticVersion.Parse("5.2.0"));

    [Fact]
    public void Resolve_ExactPin_ResolvesToItself()
        // An exact pin is its own resolution — it needs no lock entry.
        => new LockFile([]).Resolve(new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("5.0.1") })
            .Value.ShouldBe(SemanticVersion.Parse("5.0.1"));

    [Fact]
    public void Resolve_Range_ResolvesToTheLockedPin()
    {
        var lockFile = new LockFile([new LockedPlugin { Source = new PackageId("NSchema.Postgres"), Version = SemanticVersion.Parse("5.3.1") }]);

        lockFile.Resolve(new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("[5.0,6.0)") })
            .Value.ShouldBe(SemanticVersion.Parse("5.3.1"));
    }

    [Fact]
    public void Resolve_UnlockedRange_IsAnError()
        => new LockFile([]).Resolve(new PackageReference { Source = "NSchema.Postgres", Version = VersionRange.Parse("[5.0,6.0)") })
            .IsFailure.ShouldBeTrue();
}
