using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Init;

/// <summary>
/// Configuration for the init command: the provider and backend plugins to restore.
/// </summary>
internal sealed class InitConfiguration : IBindable
{
    /// <summary>
    /// The provider plugin to restore; <see langword="null"/> when none is configured.
    /// </summary>
    public PluginReference? Provider { get; set; }

    /// <summary>
    /// The state backend; only its plugin (if any) needs restoring — the built-in file store does not.
    /// </summary>
    public StateConfig? State { get; set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        Provider = project.Provider;
        State = project.State;
    }
}
