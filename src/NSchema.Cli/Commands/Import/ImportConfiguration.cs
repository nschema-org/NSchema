using System.CommandLine;
using System.Text.Json.Serialization;
using NSchema.Cli.Configuration;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;

namespace NSchema.Cli.Commands.Import;

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
    public ImportTargetConfig ImportTarget { get; init; } = new();

    /// <summary>
    /// Optional filter limiting the import to specific database schema namespaces.
    /// </summary>
    public string[]? Scope { get; private set; }

    public void Bind(ParseResult result)
    {
        Provider.Bind(result);
        ImportTarget.Bind(result);
        CommonOptions.Scope.Bind(result, s => Scope = s);
    }
}
