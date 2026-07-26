using NSchema.Configuration.Domain;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.New;

/// <summary>
/// A plugin a scaffolded project declares: the label it is referenced by, and the package version resolved for it.
/// </summary>
/// <param name="Label">The local name the configuration refers to the plugin by.</param>
/// <param name="Source">The plugin's NuGet package id.</param>
/// <param name="Version">The version resolved for it.</param>
internal sealed record ResolvedPlugin(PluginLabel Label, PackageId Source, SemanticVersion Version);
