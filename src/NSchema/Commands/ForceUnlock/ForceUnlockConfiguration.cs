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

    /// <summary>
    /// Whether to skip the interactive confirmation prompt before forcibly releasing the lock.
    /// </summary>
    public bool Force { get; private set; }

    public void Bind(ParseResult result)
    {
        // The state store (and therefore its lock) is defined entirely in nschema.json; only the --force flag is
        // layered from the command line.
        ForceUnlockOptions.Force.Bind(result, f => Force = f);
    }
}
