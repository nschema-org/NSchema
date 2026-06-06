using System.CommandLine;
using NSchema.Cli.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Cli.Configuration.Import;

internal static class ImportTargetOptions
{
    public static readonly Option<string> Output = new("--output")
    {
        Description = "Output path for imported schema files. A file path for --partition None; a directory root for Schema or Table.",
    };

    public static readonly Option<SchemaFormat> Format = new("--format")
    {
        Description = "Format for the generated schema files: yaml (default) or json.",
    };

    public static readonly Option<ImportPartitionMode> Partition = new("--partition")
    {
        Description = "How to split the imported schema across files: None (single file, default), Schema (one per namespace), Table (one per table).",
    };
}
