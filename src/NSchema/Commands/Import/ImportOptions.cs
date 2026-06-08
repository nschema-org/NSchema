using System.CommandLine;
using NSchema.Configuration;
using NSchema.Configuration.Binding;
using NSchema.Configuration.Schema;
using NSchema.Operations.Import;

namespace NSchema.Commands.Import;

internal static class ImportOptions
{
    public static readonly OptionBinding<string> PostgresConnectionString = OptionBinding.Create<string>()
        .FromEnvironmentVariable(EnvironmentVariables.PostgresConnectionString);

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
        Scope.Option,
        Tables.Option,
        Output.Option,
        Format.Option,
        Partition.Option,
    ];
}
