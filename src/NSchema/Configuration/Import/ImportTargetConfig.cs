using NSchema.Configuration.Schema;
using NSchema.Operations.Import;

namespace NSchema.Configuration.Import;

internal sealed class ImportTargetConfig
{
    /// <summary>
    /// The file to write to when <see cref="Partition"/> is <see cref="ImportPartitionMode.None"/>.
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// The directory to write into when <see cref="Partition"/> is <see cref="ImportPartitionMode.Schema"/> or <see cref="ImportPartitionMode.Table"/>.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// The format for generated schema files.
    /// </summary>
    public SchemaFormat Format { get; set; } = SchemaFormat.Yaml;

    /// <summary>
    /// Controls how the imported schema is split across output files.
    /// </summary>
    public ImportPartitionMode Partition { get; set; } = ImportPartitionMode.None;
}
