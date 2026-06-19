using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Provider;
using NSchema.Configuration.State;

namespace NSchema.Commands.Drift;

/// <summary>
/// configuration for the drift command.
/// </summary>
internal sealed class DriftConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema the recorded state is compared against.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The state store holding the recorded schema the live database is compared against.
    /// </summary>
    public StateConfig State { get; init; } = new();

    /// <summary>
    /// Optional scope filter limiting the drift check to specific database schemas (namespaces).
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// Whether to return the detailed exit code (<c>2</c> when drift is detected).
    /// </summary>
    public bool DetailedExitCode { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider.Bind(project, cli);
        State.Bind(project, cli);
        DriftOptions.Scope.Bind(project, cli, s => Scope = s);
        DriftOptions.DetailedExitCode.Bind(project, cli, d => DetailedExitCode = d);
    }
}
