using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
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
    public PluginReference? Database { get; set; }

    /// <summary>
    /// The state backend; only its plugin (if any) is a plugin — the file store is built in.
    /// </summary>
    public StateConfiguration? State { get; set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        Database = project.Database;
        State = project.State;
    }
}
