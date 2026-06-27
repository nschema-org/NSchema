using NSchema.Configuration.Plugins;

namespace NSchema.Tests.Configuration.Plugins;

public sealed class PluginCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nschema-cache-tests", Guid.NewGuid().ToString("N"));
    private readonly PluginCache _sut;

    public PluginCacheTests() => _sut = new PluginCache(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    // Seeds a restored plugin by writing the publish/<id>.dll marker that List/Contains probe for.
    private void Seed(string packageId, string version)
    {
        var publish = _sut.PublishDirectory(packageId, version);
        Directory.CreateDirectory(publish);
        File.WriteAllText(Path.Combine(publish, packageId + ".dll"), "x");
    }

    [Fact]
    public void List_MissingRoot_ReturnsEmpty()
    {
        // Act + Assert — nothing restored yet, and the root may not even exist.
        _sut.List().ShouldBeEmpty();
    }

    [Fact]
    public void List_ReturnsRestoredVersions_OrderedByPackageThenVersion()
    {
        // Arrange
        Seed("NSchema.Postgres", "4.1.0");
        Seed("NSchema.Postgres", "4.0.0");
        Seed("NSchema.Aws", "4.0.0");

        // Act
        var listed = _sut.List();

        // Assert
        listed.Select(p => (p.PackageId, p.Version))
            .ShouldBe([("NSchema.Aws", "4.0.0"), ("NSchema.Postgres", "4.0.0"), ("NSchema.Postgres", "4.1.0")]);
    }

    [Fact]
    public void List_SkipsResolveScratchDirAndHalfFinishedRestores()
    {
        // Arrange — one real restore, the _resolve scratch dir, and a version dir with no plugin assembly.
        Seed("NSchema.Postgres", "4.0.0");
        Directory.CreateDirectory(_sut.ResolveDirectory("NSchema.Postgres"));
        Directory.CreateDirectory(_sut.PublishDirectory("NSchema.Postgres", "4.9.0"));

        // Act + Assert
        _sut.List().ShouldHaveSingleItem().Version.ShouldBe("4.0.0");
    }

    [Fact]
    public void Contains_TrueOnlyForARestoredVersion()
    {
        // Arrange
        Seed("NSchema.Postgres", "4.0.0");

        // Act + Assert
        _sut.Contains("NSchema.Postgres", "4.0.0").ShouldBeTrue();
        _sut.Contains("NSchema.Postgres", "4.1.0").ShouldBeFalse();
    }

    [Fact]
    public void Remove_SpecificVersion_RemovesOnlyThatVersion()
    {
        // Arrange
        Seed("NSchema.Postgres", "4.0.0");
        Seed("NSchema.Postgres", "4.1.0");

        // Act
        var removed = _sut.Remove("NSchema.Postgres", "4.0.0");

        // Assert
        removed.ShouldHaveSingleItem().Version.ShouldBe("4.0.0");
        _sut.List().ShouldHaveSingleItem().Version.ShouldBe("4.1.0");
    }

    [Fact]
    public void Remove_NoVersion_RemovesEveryVersionAndPrunesThePackageDir()
    {
        // Arrange — two restored versions plus a leftover _resolve scratch dir.
        Seed("NSchema.Postgres", "4.0.0");
        Seed("NSchema.Postgres", "4.1.0");
        Directory.CreateDirectory(_sut.ResolveDirectory("NSchema.Postgres"));

        // Act
        var removed = _sut.Remove("NSchema.Postgres", version: null);

        // Assert
        removed.Select(p => p.Version).ShouldBe(["4.0.0", "4.1.0"], ignoreOrder: true);
        _sut.List().ShouldBeEmpty();
        // The whole package folder is pruned once empty, taking the _resolve scratch dir with it.
        Directory.Exists(System.IO.Path.Combine(_root, "NSchema.Postgres")).ShouldBeFalse();
    }

    [Fact]
    public void Clear_RemovesEveryEntryAndReturnsWhatWasPresent()
    {
        // Arrange — two packages plus a leftover scratch dir.
        Seed("NSchema.Postgres", "4.0.0");
        Seed("NSchema.Aws", "4.0.0");
        Directory.CreateDirectory(_sut.ResolveDirectory("NSchema.Postgres"));

        // Act
        var cleared = _sut.Clear();

        // Assert
        cleared.Select(p => p.PackageId).ShouldBe(["NSchema.Aws", "NSchema.Postgres"], ignoreOrder: true);
        _sut.List().ShouldBeEmpty();
        Directory.Exists(_root).ShouldBeFalse();
    }

    [Fact]
    public void Clear_EmptyCache_ReturnsEmpty()
    {
        // Act + Assert — nothing to clear, and no throw on a missing root.
        _sut.Clear().ShouldBeEmpty();
    }

    [Fact]
    public void Remove_NothingMatches_ReturnsEmptyAndLeavesTheCacheIntact()
    {
        // Arrange
        Seed("NSchema.Postgres", "4.0.0");

        // Act
        var removed = _sut.Remove("NSchema.Postgres", "9.9.9");

        // Assert
        removed.ShouldBeEmpty();
        _sut.List().ShouldHaveSingleItem();
    }
}
