namespace NSchema.Configuration.Plugins;

/// <summary>
/// Owns the on-disk layout of the plugin cache (<c>~/.nschema/plugins</c>).
/// </summary>
internal sealed class PluginCache(string? root = null)
{
    // ResolveLatestVersion floats a restore into this subdirectory of a package's folder. It is not a real pinned
    // version, so listings skip it.
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

    public string VersionDirectory(string packageId, string version) => Path.Combine(Root, packageId, version);

    public string PublishDirectory(string packageId, string version) =>
        Path.Combine(VersionDirectory(packageId, version), PublishDirectoryName);

    public string ProjectDirectory(string packageId, string version) =>
        Path.Combine(VersionDirectory(packageId, version), ProjectDirectoryName);

    public string StagingDirectory(string packageId, string version) =>
        Path.Combine(VersionDirectory(packageId, version), StagingDirectoryName);

    public string ResolveDirectory(string packageId) => Path.Combine(Root, packageId, ResolveDirectoryName);

    /// <summary>
    /// The lock file serializing a given version's restore. Its parent (the version directory) must exist first.
    /// </summary>
    public string LockFile(string packageId, string version) =>
        Path.Combine(VersionDirectory(packageId, version), LockFileName);

    /// <summary>
    /// The lock file serializing latest-version resolution for a package. Its parent (the resolve directory) must
    /// exist first.
    /// </summary>
    public string ResolveLockFile(string packageId) => Path.Combine(ResolveDirectory(packageId), LockFileName);

    // A plugin counts as restored once its own assembly sits in the publish closure — the same probe the loader makes.
    public string PluginAssembly(string packageId, string version) =>
        Path.Combine(PublishDirectory(packageId, version), packageId + ".dll");

    /// <summary>
    /// Whether the given package version has been restored into the cache.
    /// </summary>
    public bool Contains(string packageId, string version) => File.Exists(PluginAssembly(packageId, version));

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
            var packageId = Path.GetFileName(packageDir);
            foreach (var versionDir in Directory.EnumerateDirectories(packageDir))
            {
                var version = Path.GetFileName(versionDir);

                // Skip the _resolve scratch dir and any half-finished restore that never produced the plugin assembly.
                if (version == ResolveDirectoryName || !File.Exists(Path.Combine(versionDir, PublishDirectoryName, packageId + ".dll")))
                {
                    continue;
                }

                cached.Add(new CachedPlugin(packageId, version, versionDir, DirectorySize(versionDir)));
            }
        }

        return cached
            .OrderBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Removes a package from the cache: a single <paramref name="version"/>, or every restored version when it is
    /// <see langword="null"/>. Returns the entries removed (empty when nothing matched).
    /// </summary>
    public IReadOnlyList<CachedPlugin> Remove(string packageId, string? version)
    {
        var removed = List()
            .Where(p => p.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)
                        && (version is null || p.Version.Equals(version, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var entry in removed)
        {
            Directory.Delete(entry.Path, recursive: true);
        }

        // Once no restored versions remain, drop the whole package folder so leftovers (the _resolve scratch dir, a
        // half-finished restore) don't linger and the cache listing stays clean.
        var packageDir = Path.Combine(Root, packageId);
        if (Directory.Exists(packageDir)
            && !List().Any(p => p.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase)))
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
