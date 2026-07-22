using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Services.Reporting;

namespace NSchema.Commands.Plugin.Update;

internal static class PluginUpdateCommand
{
    private static readonly Argument<string?> _labelArgument = new("plugin")
    {
        Description = "The label of the plugin to update; omit to update every plugin declared with a range.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static Command Create()
    {
        var command = new Command("update", "Re-resolve declared version ranges to their highest available version and rewrite the lockfile.");
        command.Arguments.Add(_labelArgument);
        command.SetAction(Run);
        return command;
    }

    private static async Task Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var environment = ConfigurationFactory.ResolveEnvironment(parseResult);
        ConfigurationFactory.ApplyWorkingDirectory(parseResult);
        var root = Directory.GetCurrentDirectory();
        var label = parseResult.GetValue(_labelArgument);

        var messenger = ReporterFactory.CreateMessenger(parseResult);
        var loader = new PluginLoader();

        // A named plugin resolves to the one package that label declares; only that package is re-resolved, so the
        // other pins stay exactly as the lockfile records them.
        var target = await ResolveTarget(root, environment, label, cancellationToken);
        if (label is not null && target is null)
        {
            messenger.Announce($"'{label}' is pinned to an exact version; there is no range to update.");
            return;
        }

        var existing = (await LockFileManager.Read(ProjectConfigurationReader.LockFilePath(root), cancellationToken)).Require();
        var configuration = await ProjectConfigurationReader.Refresh(root, environment, existing, loader, source => target is null || source == target, cancellationToken);

        var pins = configuration.ResolvedPlugins();
        var changed = target is null ? pins : pins.Where(pin => pin.Source == target).ToList();
        if (changed.Count == 0)
        {
            messenger.Announce($"Nothing to update: no plugins are declared.");
            return;
        }

        var written = await LockFileManager.Write(ProjectConfigurationReader.LockFilePath(root), new LockFile(pins), cancellationToken);
        if (written.IsFailure)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, written.Errors.Select(error => error.Message)));
        }

        foreach (var pin in changed)
        {
            var previous = existing.Find(pin.Source)?.Version;
            if (previous is null)
            {
                messenger.Success($"{pin.Source} locked at {pin.Version}");
            }
            else if (!previous.Equals(pin.Version))
            {
                messenger.Success($"{pin.Source} {previous} → {pin.Version}");
            }
            else
            {
                messenger.Announce($"{pin.Source} {pin.Version} (already up to date)");
            }
        }

        // Leave the project usable: bring the (possibly new) pinned versions into the cache.
        var references = new List<PluginReference>();
        if (configuration.Database is { } provider && (target is null || provider.PackageId == target))
        {
            references.Add(provider);
        }
        if (configuration.State?.Plugin is { } backend && (target is null || backend.PackageId == target))
        {
            references.Add(backend);
        }

        loader.Restore(references, messenger);
    }

    // Returns the package to update (null when every range is updated), or null when a named label is an exact pin —
    // distinguished by the caller through <paramref name="label"/>.
    private static async Task<PackageId?> ResolveTarget(string root, string? environment, string? label, CancellationToken cancellationToken)
    {
        if (label is null)
        {
            return null;
        }

        var declarations = await ProjectConfigurationReader.ReadDeclarations(root, environment, cancellationToken);
        var declaration = declarations.FirstOrDefault(declaration => declaration.Label == new PluginLabel(label))
            ?? throw new InvalidOperationException($"No plugin labelled '{label}' is declared.");

        return declaration.Package.Version.IsExact ? null : declaration.Package.Source;
    }
}
