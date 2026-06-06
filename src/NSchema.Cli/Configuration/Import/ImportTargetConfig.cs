using System.CommandLine;
using NSchema.Cli.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Cli.Configuration.Import;

internal sealed class ImportTargetConfig : IConfigurable
{
    /// <summary>
    /// The output path:
    /// - A file path for <see cref="ImportPartitionMode.None"/>;
    /// - The root directory for <see cref="ImportPartitionMode.Schema"/> and <see cref="ImportPartitionMode.Table"/>.
    /// </summary>
    public string OutputPath { get; private set; } = "";

    /// <summary>
    /// The format for generated schema files.
    /// </summary>
    public SchemaFormat Format { get; private set; } = SchemaFormat.Yaml;

    /// <summary>
    /// Controls how the imported schema is split across output files.
    /// </summary>
    public ImportPartitionMode Partition { get; private set; } = ImportPartitionMode.None;

    public void Configure(ParseResult result)
    {
        if (ImportTargetOptions.Output.TryResolve(result, out var outputPath))
        {
            OutputPath = outputPath;
        }

        if (ImportTargetOptions.Format.TryResolve(result, out var format))
        {
            Format = format;
        }

        if (ImportTargetOptions.Partition.TryResolve(result, out var partition))
        {
            Partition = partition;
        }
    }
}
