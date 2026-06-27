using NSchema.Configuration.State;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// Builds the inventory of plugins a project uses (the provider and a non-file backend) from its resolved config.
/// </summary>
internal static class PluginInventory
{
    public const string ProviderRole = "provider";
    public const string BackendRole = "backend";

    /// <summary>
    /// Lists the plugins the project pins, each checked against the <paramref name="cache"/>.
    /// </summary>
    public static IReadOnlyList<ProjectPlugin> ForProject(PluginReference? provider, StateConfig? state, PluginCache cache)
    {
        var plugins = new List<ProjectPlugin>();

        if (provider is not null)
        {
            plugins.Add(Describe(ProviderRole, provider, cache));
        }

        if (state?.Plugin is { } backend)
        {
            plugins.Add(Describe(BackendRole, backend, cache));
        }

        return plugins;
    }

    private static ProjectPlugin Describe(string role, PluginReference reference, PluginCache cache)
    {
        var restored = cache.Contains(reference.PackageId, reference.Version);
        return new ProjectPlugin(
            role,
            reference.Label,
            reference.PackageId,
            reference.Version,
            restored,
            restored ? cache.VersionDirectory(reference.PackageId, reference.Version) : null);
    }
}
