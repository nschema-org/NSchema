using NSchema.Configuration.Domain;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// The plugin lockfile (<c>nschema.lock</c>): the exact versions the project has resolved and locked its
/// plugins to. Named for the file, and distinct from the state lock.
/// </summary>
/// <param name="Plugins">The locked plugins, in source order.</param>
public sealed record LockFile(IReadOnlyList<LockedPlugin> Plugins)
{
    /// <summary>
    /// An empty lockfile — nothing locked yet.
    /// </summary>
    public static LockFile Empty { get; } = new([]);

    /// <summary>
    /// The entry locking <paramref name="source"/>, or <see langword="null"/> when it is unlocked.
    /// </summary>
    public LockedPlugin? Find(PackageId source) => Plugins.FirstOrDefault(plugin => plugin.Source == source);

    /// <summary>
    /// The lockfile with <paramref name="versions"/> applied over it.
    /// </summary>
    /// <param name="versions">The pins to apply.</param>
    public LockFile With(IReadOnlyList<LockedPlugin> versions) => new([
        .. Plugins.Select(existing => versions.FirstOrDefault(pin => pin.Source == existing.Source) ?? existing),
        .. versions.Where(pin => Find(pin.Source) is null),
    ]);

    /// <summary>
    /// Resolves <paramref name="package"/> to the concrete version to use: an exact pin is its own resolution;
    /// a range resolves to its locked pin, and is an error when the lockfile does not carry one.
    /// </summary>
    public Result<SemanticVersion> Resolve(PackageReference package)
    {
        if (package.Version.ExactVersion is { } exact)
        {
            return Result.Success(exact);
        }

        return Find(package.Source)?.Version is { } locked
            ? Result.Success(locked)
            : PluginDiagnostics.PluginNotLocked(package.Source, package.Version);
    }
}
