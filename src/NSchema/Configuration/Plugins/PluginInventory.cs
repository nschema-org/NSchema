using NSchema.Configuration.State;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Builds the inventory of plugins a project uses (the provider and a non-file backend) from its resolved config.
/// </summary>
internal static class PluginInventory
{
    public const string DatabaseRole = "database";
    public const string StateRole = "state";

    /// <summary>
    /// Lists the plugins the project pins, each checked against the <paramref name="cache"/>.
    /// </summary>
    public static IReadOnlyList<ProjectPlugin> ForProject(PluginReference? provider, StateConfiguration? state, PluginCache cache)
    {
        var plugins = new List<ProjectPlugin>();

        if (provider is not null)
        {
            plugins.Add(Describe(DatabaseRole, provider, cache));
        }

        if (state?.Plugin is { } backend)
        {
            plugins.Add(Describe(StateRole, backend, cache));
        }

        return plugins;
    }

    private static ProjectPlugin Describe(string role, PluginReference reference, PluginCache cache)
    {
        // A path plugin is never in the cache; reporting it as un-restored would send someone off to restore
        // something that is already sitting where they built it.
        if (reference.Origin is ResolvedPath path)
        {
            return new ProjectPlugin(role, reference.Label, reference.PackageId, reference.Version, true, path.AssemblyPath);
        }

        var restored = cache.Contains(reference.PackageId, reference.Version!);
        return new ProjectPlugin(
            role,
            reference.Label,
            reference.PackageId,
            reference.Version,
            restored,
            restored ? cache.VersionDirectory(reference.PackageId, reference.Version!) : null);
    }
}
