using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Provider;
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
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store whose reachability, recorded state, and lock doctor probes, when one is declared.
    /// </summary>
    public StateConfig State { get; init; } = new();

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider.Bind(project, cli);
        State.Bind(project, cli);
    }
}
