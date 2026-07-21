using NSchema.Configuration.Model;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Owns the on-disk layout of the plugin cache (<c>~/.nschema/plugins</c>).
/// </summary>
internal sealed class PluginCache(string? root = null)
{
    // A leftover scratch dir some earlier resolutions floated into; it is not a real pinned version, so listings skip it.
    internal const string ResolveDirectoryName = "_resolve";

    private const string PublishDirectoryName = "publish";
    private const string ProjectDirectoryName = "proj";

    // The restore publishes here first, then atomically renames it onto the publish dir.
    private const string StagingDirectoryName = ".staging";

    // Per-version lock file guarding the restore (see FileLock). Lives beside publish/proj inside the version dir.
    private const string LockFileName = ".lock";

    /// <summary>
    /// The cache root, e.g. <c>~/.nschema/plugins</c>.
    /// </summary>
    public string Root { get; } = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nschema", "plugins");

    public string VersionDirectory(PackageId packageId, SemanticVersion version) => Path.Combine(Root, packageId.Value, version.ToString());

    public string PublishDirectory(PackageId packageId, SemanticVersion version) =>
        Path.Combine(VersionDirectory(packageId, version), PublishDirectoryName);

    public string ProjectDirectory(PackageId packageId, SemanticVersion version) =>
        Path.Combine(VersionDirectory(packageId, version), ProjectDirectoryName);

    public string StagingDirectory(PackageId packageId, SemanticVersion version) =>
        Path.Combine(VersionDirectory(packageId, version), StagingDirectoryName);

    /// <summary>
    /// The lock file serializing a given version's restore. Its parent (the version directory) must exist first.
    /// </summary>
    public string LockFile(PackageId packageId, SemanticVersion version) =>
        Path.Combine(VersionDirectory(packageId, version), LockFileName);

    // A plugin counts as restored once its own assembly sits in the publish closure — the same probe the loader makes.
    public string PluginAssembly(PackageId packageId, SemanticVersion version) =>
        Path.Combine(PublishDirectory(packageId, version), packageId.Value + ".dll");

    /// <summary>
    /// Whether the given package version has been restored into the cache.
    /// </summary>
    public bool Contains(PackageId packageId, SemanticVersion version) => File.Exists(PluginAssembly(packageId, version));

    /// <summary>
    /// Enumerates the restored plugins in the cache, ordered by package then version.
    /// </summary>
    public IReadOnlyList<CachedPlugin> List()
    {
        if (!Directory.Exists(Root))
        {
            return [];
        }

        var cached = new List<CachedPlugin>();
        foreach (var packageDir in Directory.EnumerateDirectories(Root))
        {
            var packageName = Path.GetFileName(packageDir);
            if (!PackageId.IsValid(packageName))
            {
                continue;
            }

            var packageId = new PackageId(packageName);
            foreach (var versionDir in Directory.EnumerateDirectories(packageDir))
            {
                var versionName = Path.GetFileName(versionDir);

                // Skip the _resolve scratch dir, any dir that isn't a version, and any half-finished restore that
                // never produced the plugin assembly.
                if (versionName == ResolveDirectoryName
                    || !SemanticVersion.TryParse(versionName, out var version)
                    || !File.Exists(Path.Combine(versionDir, PublishDirectoryName, packageName + ".dll")))
                {
                    continue;
                }

                cached.Add(new CachedPlugin(packageId, version, versionDir, DirectorySize(versionDir)));
            }
        }

        return cached
            .OrderBy(p => p.PackageId.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Version)
            .ToList();
    }

    /// <summary>
    /// Removes a package from the cache: a single <paramref name="version"/>, or every restored version when it is
    /// <see langword="null"/>. Returns the entries removed (empty when nothing matched).
    /// </summary>
    public IReadOnlyList<CachedPlugin> Remove(PackageId packageId, SemanticVersion? version)
    {
        var removed = List()
            .Where(p => p.PackageId == packageId && (version is null || p.Version == version))
            .ToList();

        foreach (var entry in removed)
        {
            Directory.Delete(entry.Path, recursive: true);
        }

        // Once no restored versions remain, drop the whole package folder so leftovers (the _resolve scratch dir, a
        // half-finished restore) don't linger and the cache listing stays clean.
        var packageDir = Path.Combine(Root, packageId.Value);
        if (Directory.Exists(packageDir) && List().All(p => p.PackageId != packageId))
        {
            Directory.Delete(packageDir, recursive: true);
        }

        return removed;
    }

    /// <summary>
    /// Removes every entry from the cache, returning the restored plugins that were present.
    /// </summary>
    public IReadOnlyList<CachedPlugin> Clear()
    {
        var cleared = List();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }

        return cleared;
    }

    private static long DirectorySize(string path) =>
        new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
}
