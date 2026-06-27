using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Plugin.List;

/// <summary>
/// Configuration for <c>plugin list</c>: the provider and backend the project pins.
/// </summary>
internal sealed class PluginListConfiguration : IBindable
{
    /// <summary>
    /// The provider plugin; <see langword="null"/> when none is configured.
    /// </summary>
    public PluginReference? Provider { get; set; }

    /// <summary>
    /// The state backend; only its plugin (if any) is a plugin — the file store is built in.
    /// </summary>
    public StateConfig? State { get; set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider = project.Provider;
        State = project.State;
    }
}
