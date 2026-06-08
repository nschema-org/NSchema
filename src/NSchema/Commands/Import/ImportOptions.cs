using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Provider;
using NSchema.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Commands.Import;

internal static class ImportOptions
{
    public static readonly OptionBinding<ProviderType> Provider = OptionBinding.Create<ProviderType>()
        .FromOption("--provider")
        .FromEnvironmentVariable(EnvironmentVariables.Provider)
        .WithDescription("Database provider whose live schema is read and written out as source files (e.g. postgres).");

    public static readonly OptionBinding<string> ConnectionString = OptionBinding.Create<string>()
        .FromOption("--connection-string")
        .FromEnvironmentVariable(EnvironmentVariables.ConnectionString)
        .WithDescription("Connection string for the database whose schema is imported.");

    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the import to specific database schema namespaces. May be specified multiple times.");

    public static readonly OptionBinding<string[]> Tables = OptionBinding.Create<string[]>()
        .FromOption("--tables")
        .AllowMultipleArguments()
        .WithDescription("Limit the import to specific database tables. May be specified multiple times.");

    public static readonly OptionBinding<string> Output = OptionBinding.Create<string>()
        .FromOption("--output")
        .WithDescription("Output path for imported schema files. A file path for --partition None; a directory root for Schema or Table.");

    public static readonly OptionBinding<SchemaFormat> Format = OptionBinding.Create<SchemaFormat>()
        .FromOption("--format")
        .WithDescription("Format for the generated schema files: yaml (default) or json.");

    public static readonly OptionBinding<ImportPartitionMode> Partition = OptionBinding.Create<ImportPartitionMode>()
        .FromOption("--partition")
        .WithDescription("How to split the imported schema across files: None (single file, default), Schema (one per namespace), Table (one per table).");

    public static IEnumerable<Option> All =>
    [
        Provider.Option,
        ConnectionString.Option,
        Scope.Option,
        Tables.Option,
        Output.Option,
        Format.Option,
        Partition.Option,
    ];
}
