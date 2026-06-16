using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Dsl;
using NSchema.Configuration.Import;
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
    /// Where and how to write the imported schema files.
    /// </summary>
    [JsonIgnore]
    public ImportTargetConfig Target { get; init; } = new();

    /// <summary>
    /// Optional filter limiting the import to specific database schema namespaces.
    /// </summary>
    public string[]? Scope { get; private set; }

    /// <summary>
    /// Optional filter limiting the import to specific table names. When <see langword="null"/>, all tables are imported.
    /// </summary>
    public string[]? Tables { get; private set; }

    public void Bind(DslProjectConfig project, ParseResult cli)
    {
        ImportOptions.PostgresConnectionString.Bind(project, cli, cs => Provider.EnsurePostgres().ConnectionString = cs);
        ImportOptions.CommandTimeout.Bind(project, cli, t => Provider.EnsurePostgres().CommandTimeout = t);
        ImportOptions.Scope.Bind(project, cli, s => Scope = s);
        ImportOptions.Tables.Bind(project, cli, t => Tables = t);
        ImportOptions.OutputFile.Bind(project, cli, o => Target.OutputFile = o);
        ImportOptions.OutputDirectory.Bind(project, cli, o => Target.OutputDirectory = o);
        ImportOptions.Partition.Bind(project, cli, p => Target.Partition = p);
    }
}
