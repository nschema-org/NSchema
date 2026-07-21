using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Destroy;

/// <summary>
/// Configuration for the destroy command.
/// </summary>
internal sealed class DestroyConfiguration : IBindable
{
    /// <summary>
    /// The database provider the teardown is generated and executed against.
    /// </summary>
    public PluginReference? Database { get; set; }

    /// <summary>
    /// The state store the managed schema is read from and the post-destroy snapshot is written to.
    /// </summary>
    public StateConfiguration? State { get; set; }

    /// <summary>
    /// Whether to skip the interactive confirmation prompt before tearing down the schema.
    /// </summary>
    public bool AutoApprove { get; private set; }

    /// <summary>
    /// Whether to tear down without acquiring the state lock (<c>--no-lock</c>).
    /// </summary>
    public bool NoLock { get; private set; }

    /// <summary>
    /// Whether to run against an in-memory state store instead of a configured <c>STATE</c> store.
    /// </summary>
    // internal set: bound via Bind, but the validator's presence rules branch on it, so tests set it directly.
    public bool Ephemeral { get; internal set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        Database = project.Database;
        State = project.State;
        DestroyOptions.AutoApprove.Bind(cli, a => AutoApprove = a);
        DestroyOptions.NoLock.Bind(cli, n => NoLock = n);
        DestroyOptions.Ephemeral.Bind(cli, e => Ephemeral = e);
    }
}
