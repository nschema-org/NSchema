using NSchema.Cli.Configuration.Import;
using NSchema.Cli.Configuration.Provider;

namespace NSchema.Cli.Commands.Import;

/// <summary>
/// Configuration for the import command.
/// </summary>
internal sealed class ImportConfiguration
{
    /// <summary>
    /// The database provider supplying the live schema to import.
    /// </summary>
    public required ProviderConfig Provider { get; init; }

    /// <summary>
    /// Where and how to write the imported schema files.
    /// </summary>
    public required ImportTargetConfig ImportTarget { get; init; }

    /// <summary>
    /// Optional filter limiting the import to specific database schema namespaces.
    /// </summary>
    public string[]? Scope { get; init; }
}
