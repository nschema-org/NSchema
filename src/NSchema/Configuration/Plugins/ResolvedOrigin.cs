namespace NSchema.Configuration.Plugins;

/// <summary>
/// Where a plugin will be loaded from, once its declaration has been resolved.
/// </summary>
/// <remarks>
/// The resolved counterpart of <see cref="PluginOrigin"/>: a declared version range has become a concrete version,
/// and a declared path has become an absolute one. Closed for the same reason, so the loader's branch is exhaustive.
/// </remarks>
internal abstract record ResolvedOrigin
{
    private protected ResolvedOrigin()
    {
    }
}
