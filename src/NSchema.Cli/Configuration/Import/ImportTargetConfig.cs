using System.CommandLine;
using NSchema.Cli.Configuration.Schema;
using NSchema.Import;

namespace NSchema.Cli.Configuration.Import;

internal sealed class ImportTargetConfig : IBindable
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

    public void Bind(ParseResult result)
    {
        ImportTargetOptions.Output.Bind(result, p => OutputPath = p);
        ImportTargetOptions.Format.Bind(result, f => Format = f);
        ImportTargetOptions.Partition.Bind(result, p => Partition = p);
    }
}
