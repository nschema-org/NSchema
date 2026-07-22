using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Init;

/// <summary>
/// Resolves declared plugin versions, writes the lockfile, and restores the resolved plugins into the cache — the
/// shared body of <c>nschema init</c> and the init step <c>scaffold</c> runs.
/// </summary>
internal static class ProjectInitializer
{
    public static async Task Initialize(string root, string? environment, PluginLoader loader, IConsoleMessenger messenger, CancellationToken cancellationToken)
    {
        // Keep versions already locked, resolving only ranges that are new or unlocked — the lockfile is respected,
        // not silently upgraded (that is 'plugin update').
        var existing = (await LockFileManager.Read(ProjectConfigurationReader.LockFilePath(root), cancellationToken)).Require();
        var configuration = await ProjectConfigurationReader.Refresh(root, environment, existing, loader, refresh: null, cancellationToken);

        // The file backend is built in, so only the provider and a plugin-backed state store need restoring.
        var references = new List<PluginReference>();
        if (configuration.Database is { } database)
        {
            references.Add(database);
        }
        if (configuration.State?.Plugin is { } state)
        {
            references.Add(state);
        }

        if (references.Count == 0)
        {
            messenger.Announce($"Nothing to restore: no database or state plugin is configured.");
            return;
        }

        // Persist the resolved pins so a later plan/apply reads exactly the versions restored here.
        var plugins = configuration.ResolvedPlugins();
        if (plugins.Count > 0)
        {
            var written = await LockFileManager.Write(ProjectConfigurationReader.LockFilePath(root), new LockFile(plugins), cancellationToken);
            if (written.IsFailure)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, written.Errors.Select(error => error.Message)));
            }
        }

        loader.Restore(references, messenger);
    }
}
