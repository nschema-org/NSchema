using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Ddl;
using NSchema.Configuration.Provider;

namespace NSchema.Commands.Import;

/// <summary>
/// Configuration for the import command.
/// </summary>
internal sealed class ImportConfiguration : IBindable
{
    /// <summary>
    /// The database provider supplying the live schema to import.
    /// </summary>
    public ProviderConfig Provider { get; init; } = new();

    /// <summary>
    /// The directory to write into.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Optional filter limiting the import to specific database schema namespaces.
    /// </summary>
    public string[]? Scope { get; private set; }

    public void Bind(DdlProjectConfig project, ParseResult cli)
    {
        Provider.Bind(project, cli);
        ImportOptions.Scope.Bind(project, cli, s => Scope = s);
        ImportOptions.OutputDirectory.Bind(project, cli, o => OutputDirectory = o);
    }
}
