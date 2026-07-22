using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;
using NSchema.Configuration.State;

namespace NSchema.Commands.Doctor;

/// <summary>
/// Configuration for the doctor command.
/// </summary>
internal sealed class DoctorConfiguration : IBindable
{
    /// <summary>
    /// The database provider whose connectivity doctor probes, when one is declared.
    /// </summary>
    public PluginReference? Database { get; set; }

    /// <summary>
    /// The state store whose reachability, recorded state, and lock doctor probes, when one is declared.
    /// </summary>
    public StateConfiguration? State { get; set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        Database = project.Database;
        State = project.State;
    }
}
