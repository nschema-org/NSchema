using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Plugins;

namespace NSchema.Commands.Import;

/// <summary>
/// Configuration for the import command.
/// </summary>
internal sealed class ImportConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema to import.
    /// </summary>
    public PluginReference? Provider { get; set; }

    /// <summary>
    /// The directory to write into.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Optional filter limiting the import to specific database schema namespaces.
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// Whether to overwrite existing <c>.sql</c> files in the output directory.
    /// </summary>
    public bool Force { get; private set; }

    public void Bind(ProjectConfig project, ParseResult cli)
    {
        Provider = project.Provider;
        ImportOptions.Scope.Bind(cli, s => Scope = s);
        ImportOptions.OutputDirectory.Bind(cli, o => OutputDirectory = o);
        ImportOptions.Force.Bind(cli, f => Force = f);
    }
}
