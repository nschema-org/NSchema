using System.CommandLine;
using NSchema.Configuration.Binding;
using NSchema.Operations.Import;

namespace NSchema.Commands.Import;

internal static class ImportOptions
{
    public static readonly OptionBinding<string[]> Scope = OptionBinding.Create<string[]>()
        .FromOption("--scope")
        .AllowMultipleArguments()
        .WithDescription("Limit the import to specific database schema namespaces. May be specified multiple times.");

    public static readonly OptionBinding<string[]> Tables = OptionBinding.Create<string[]>()
        .FromOption("--tables")
        .AllowMultipleArguments()
        .WithDescription("Limit the import to specific database tables. May be specified multiple times.");

    public static readonly OptionBinding<string> OutputFile = OptionBinding.Create<string>()
        .FromOption("--output-file")
        .WithDescription("File to write the imported schema to. Use with --partition None (the default), which writes the whole schema as one document.");

    public static readonly OptionBinding<string> OutputDirectory = OptionBinding.Create<string>()
        .FromOption("--output-dir")
        .WithDescription("Directory to write the imported schema files into. Use with --partition Schema or Table.");

    public static readonly OptionBinding<ImportPartitionMode> Partition = OptionBinding.Create<ImportPartitionMode>()
        .FromOption("--partition")
        .WithDescription("How to split the imported schema across files: None (single file, default), Schema (one per namespace), Table (one per table).");

    public static IEnumerable<Option> All =>
    [
        Scope.Option,
        Tables.Option,
        OutputFile.Option,
        OutputDirectory.Option,
        Partition.Option,
    ];
}
