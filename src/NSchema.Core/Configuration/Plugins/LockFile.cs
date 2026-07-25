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
    /// Resolves <paramref name="declaration"/> to the concrete version to use: an exact pin is its own resolution;
    /// a range resolves to its locked pin, and is an error when the lockfile does not carry one.
    /// </summary>
    public Result<SemanticVersion> Resolve(PluginDeclaration declaration)
    {
        if (declaration.Package.Version.ExactVersion is { } exact)
        {
            return Result.Success(exact);
        }

        return Find(declaration.Package.Source)?.Version is { } locked
            ? Result.Success(locked)
            : PluginDiagnostics.PluginNotLocked(declaration.Package.Source, declaration.Package.Version);
    }
}
