using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Configuration.Binding;
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

    public void Bind(ParseResult result)
    {
        ImportOptions.Scope.Bind(result, s => Scope = s);
        ImportOptions.PostgresConnectionString.Bind(result, cs => Provider.EnsurePostgres().ConnectionString = cs);

        ImportOptions.Tables.Bind(result, t => Tables = t);
        ImportOptions.Format.Bind(result, f => Target.Format = f);
        ImportOptions.OutputFile.Bind(result, o => Target.OutputFile = o);
        ImportOptions.OutputDirectory.Bind(result, o => Target.OutputDirectory = o);
        ImportOptions.Partition.Bind(result, p => Target.Partition = p);
    }
}
