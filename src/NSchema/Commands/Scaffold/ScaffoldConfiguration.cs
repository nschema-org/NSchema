using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;

namespace NSchema.Commands.Scaffold;

/// <summary>
/// Configuration for the scaffold command.
/// </summary>
internal sealed class ScaffoldConfiguration : IBindable
{
    /// <summary>
    /// Whether to scaffold the project even if the directory isn't empty.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// The database provider to scaffold configuration and a sample schema for.
    /// </summary>
    public DatabaseKind Database { get; set; } = DatabaseKind.Postgres;

    /// <summary>
    /// The state backend to scaffold configuration for.
    /// </summary>
    public StateKind State { get; set; } = StateKind.File;

    /// <summary>
    /// Whether to skip the automatic <c>init</c> that resolves and locks the scaffolded plugins.
    /// </summary>
    public bool NoInit { get; set; }

    public void Bind(ProjectConfiguration project, ParseResult cli)
    {
        ScaffoldOptions.Force.Bind(cli, f => Force = f);
        ScaffoldOptions.Database.Bind(cli, p => Database = p);
        ScaffoldOptions.State.Bind(cli, b => State = b);
        ScaffoldOptions.NoInit.Bind(cli, n => NoInit = n);
    }
}
