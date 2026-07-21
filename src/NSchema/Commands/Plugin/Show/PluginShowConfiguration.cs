using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Plugin.Show;

/// <summary>
/// Configuration for <c>plugin show</c>: the project's plugins plus the label to show.
/// </summary>
internal sealed class PluginShowConfiguration : IBindable
{
    /// <summary>
    /// The provider plugin; <see langword="null"/> when none is configured.</summary>
    public PluginReference? Database { get; set; }

    /// <summary>
    /// The state backend; only its plugin (if any) is a plugin — the file store is built in.
    /// </summary>
    public StateConfiguration? State { get; set; }

    /// <summary>
    /// The label of the plugin to show (e.g. <c>postgres</c>, <c>s3</c>).
    /// </summary>
    public string? Label { get; set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        Database = project.Database;
        State = project.State;
        Label = cli.GetValue(PluginShowCommand._labelArgument);
    }
}
