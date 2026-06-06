using System.CommandLine;
using NSchema.Cli.Configuration.Binding;
using NSchema.Cli.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Cli.Configuration.Import;

internal static class ImportTargetOptions
{
    public static readonly OptionBinding<string> Output = OptionBinding.Create<string>()
        .FromOption("--output")
        .WithDescription("Output path for imported schema files. A file path for --partition None; a directory root for Schema or Table.");

    public static readonly OptionBinding<SchemaFormat> Format = OptionBinding.Create<SchemaFormat>()
        .FromOption("--format")
        .WithDescription("Format for the generated schema files: yaml (default) or json.");

    public static readonly OptionBinding<ImportPartitionMode> Partition = OptionBinding.Create<ImportPartitionMode>()
        .FromOption("--partition")
        .WithDescription("How to split the imported schema across files: None (single file, default), Schema (one per namespace), Table (one per table).");

    public static IEnumerable<Option> All => [Output.Option, Format.Option, Partition.Option];
}
