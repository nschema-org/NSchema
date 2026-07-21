using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Refresh;

/// <summary>
/// configuration for the refresh command.
/// </summary>
internal sealed class RefreshConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema.
    /// </summary>
    public PluginReference? Database { get; set; }

    /// <summary>
    /// The state store the live schema is written to.
    /// </summary>
    public StateConfiguration? State { get; set; }

    /// <summary>
    /// Whether to refresh without acquiring the state lock.
    /// </summary>
    public bool NoLock { get; private set; }

    /// <summary>
    /// Whether to replace an existing state payload that cannot be read.
    /// </summary>
    public bool Force { get; private set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        Database = project.Database;
        State = project.State;
        RefreshOptions.NoLock.Bind(cli, n => NoLock = n);
        RefreshOptions.Force.Bind(cli, f => Force = f);
    }
}
