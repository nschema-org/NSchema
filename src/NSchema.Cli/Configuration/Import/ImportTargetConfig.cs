using NSchema.Cli.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Cli.Configuration.Import;

internal sealed class ImportTargetConfig
{
    /// <summary>
    /// The output path:
    /// - A file path for <see cref="ImportPartitionMode.None"/>;
    /// - The root directory for <see cref="ImportPartitionMode.Schema"/> and <see cref="ImportPartitionMode.Table"/>.
    /// </summary>
    public string OutputPath { get; set; } = "";

    /// <summary>
    /// The format for generated schema files.
    /// </summary>
    public SchemaFormat Format { get; set; } = SchemaFormat.Yaml;

    /// <summary>
    /// Controls how the imported schema is split across output files.
    /// </summary>
    public ImportPartitionMode Partition { get; set; } = ImportPartitionMode.None;
}
