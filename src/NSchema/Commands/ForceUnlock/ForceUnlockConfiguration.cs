using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.State;

namespace NSchema.Commands.ForceUnlock;

/// <summary>
/// configuration for the force-unlock command.
/// </summary>
internal sealed class ForceUnlockConfiguration : IBindable
{
    /// <summary>
    /// The state store whose lock is forcibly released. force-unlock only touches the lock and never contacts the
    /// live database, so this is the sole input.
    /// </summary>
    public StateConfig State { get; init; } = new();

    public void Bind(ParseResult result)
    {
        // The state store (and therefore its lock) is defined entirely in nschema.json; there is nothing to layer
        // from the environment or the command line.
    }
}
